using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ICSharpCode.SharpZipLib.Zip.Compression;
using JMMServer;
using NLog;

namespace AniDBAPI.Commands
{
    public abstract class AniDBUDPCommand
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string commandText = string.Empty;
        public string socketResponse = string.Empty;
        public string errorMessage = string.Empty;
        public bool errorOccurred = false;
        public string mcommandText = string.Empty;

        public string commandID = string.Empty;
        protected string sessionID = string.Empty;
        public enAniDBCommandType commandType;
        private Encoding encoding = Encoding.ASCII;
        public int ResponseCode { get; set; }

        public Encoding Encoding
        {
            get { return encoding; }
        }

        public void ProcessCommand(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            this.sessionID = sessionID;
            Encoding changeencoding = null;
            encoding = enc;
            EndPoint RemotePoint = remoteIpEndPoint;
            mcommandText = commandText;
            errorOccurred = false;

            if (commandType != enAniDBCommandType.Ping)
            {
                if (commandType != enAniDBCommandType.Login)
                {
                    if (commandType != enAniDBCommandType.Logout && commandType != enAniDBCommandType.GetMyListStats)
                    {
                        mcommandText += "&";
                    }
                    mcommandText += "s=" + sessionID;
                }
                else
                {
                    encoding = System.Text.Encoding.ASCII;
                    changeencoding = enc;
                    string encod = changeencoding.EncodingName;
                    if (changeencoding.EncodingName.StartsWith("Unicode"))
                        encod = "utf-16";
                    mcommandText += "&enc=" + encod;
                }
            }
            bool multipart = false;
            int part = 0;
            int maxpart = 1;
            string fulldesc = "";
            string decodedstring = "";
            DateTime start = DateTime.Now;

            do
            {
                if (part > 0)
                {
                    mcommandText = mcommandText.Replace("part=" + (part - 1).ToString(), "part=" + part.ToString());
                    Thread.Sleep(2300);
                }
                if (commandType != enAniDBCommandType.Login)
                {
                    string msg = string.Format("UDP_COMMAND: {0}", mcommandText);
                    JMMService.LogToSystem(Constants.DBLogType.APIAniDBUDP, msg);
                }
                else
                {
                    //string msg = commandText.Replace(MainWindow.settings.Username, "******");
                    //msg = msg.Replace(MainWindow.settings.Password, "******");
                    //MyAnimeLog.Write("commandText: {0}", msg);
                }
                bool repeatcmd;
                int received;
                Byte[] byReceivedAdd = new Byte[2000]; // max length should actually be 1400
                do
                {
                    repeatcmd = false;
                    // Send Message
                    Byte[] SendByteAdd = Encoding.GetBytes(mcommandText.ToCharArray());

                    try
                    {
                        JMMService.LastAniDBMessage = DateTime.Now;
                        JMMService.LastAniDBUDPMessage = DateTime.Now;
                        if (commandType != enAniDBCommandType.Ping)
                            JMMService.LastAniDBMessageNonPing = DateTime.Now;
                        else
                            JMMService.LastAniDBPing = DateTime.Now;

                        soUDP.SendTo(SendByteAdd, remoteIpEndPoint);
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorException(ex.ToString(), ex);
                        //MyAnimeLog.Write(ex.ToString());
                        errorOccurred = true;
                        errorMessage = ex.ToString();
                    }


                    // Receive Response
                    received = 0;
                    try
                    {
                        //MyAnimeLog.Write("soUDP.ReceiveTimeout = {0}", soUDP.ReceiveTimeout.ToString());


                        received = soUDP.ReceiveFrom(byReceivedAdd, ref RemotePoint);
                        JMMService.LastAniDBMessage = DateTime.Now;
                        JMMService.LastAniDBUDPMessage = DateTime.Now;
                        if (commandType != enAniDBCommandType.Ping)
                            JMMService.LastAniDBMessageNonPing = DateTime.Now;
                        else
                            JMMService.LastAniDBPing = DateTime.Now;

                        //MyAnimeLog.Write("Buffer length = {0}", received.ToString());
                        if ((received > 2) && (byReceivedAdd[0] == 0) && (byReceivedAdd[1] == 0))
                        {
                            //deflate
                            Byte[] buff = new byte[65536];
                            Byte[] input = new byte[received - 2];
                            Array.Copy(byReceivedAdd, 2, input, 0, received - 2);
                            Inflater inf = new Inflater(false);
                            inf.SetInput(input);
                            inf.Inflate(buff);
                            byReceivedAdd = buff;
                            received = (int) inf.TotalOut;
                        }
                    }
                    catch (SocketException sex)
                    {
                        // most likely we have timed out
                        logger.ErrorException(sex.ToString(), sex);
                        errorOccurred = true;
                        errorMessage = sex.ToString();
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorException(ex.ToString(), ex);
                        errorOccurred = true;
                        errorMessage = ex.ToString();
                    }
                    if ((commandType == enAniDBCommandType.Login) && (byReceivedAdd[0] == 0xFE) &&
                        (byReceivedAdd[1] == 0xFF) &&
                        (byReceivedAdd[3] == 53) && (byReceivedAdd[5] != 53) &&
                        !Encoding.EncodingName.ToLower().StartsWith("unicode") && (changeencoding != null) &&
                        changeencoding.EncodingName.ToLower().StartsWith("unicode"))
                    {
                        //Previous Session used utf-16 and was not logged out, AniDB was not yet issued a timeout.
                        //AUTH command was not understand because it was encoded in ASCII.
                        encoding = changeencoding;
                        repeatcmd = true;
                    }
                } while (repeatcmd);

                if (!errorOccurred)
                {
                    if (changeencoding != null)
                        encoding = changeencoding;
                    System.Text.Encoding enco;
                    if ((byReceivedAdd[0] == 0xFE) && (byReceivedAdd[1] == 0xFF))
                        enco = encoding;
                    else
                        enco = Encoding.ASCII;
                    decodedstring = enco.GetString(byReceivedAdd, 0, received);

                    if (decodedstring[0] == 0xFEFF) // remove BOM
                        decodedstring = decodedstring.Substring(1);
                    if (commandType == enAniDBCommandType.GetAnimeDescription ||
                        commandType == enAniDBCommandType.GetReview)
                    {
                        //Lets handle multipart
                        part++;
                        string[] sp1 = decodedstring.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);

                        if (sp1[0].StartsWith("233 ANIMEDESC") || sp1[0].StartsWith("233  ANIMEDESC"))
                        {
                            string[] sp2 = sp1[1].Split('|');
                            fulldesc += sp2[2];
                            maxpart = int.Parse(sp2[1]);
                        }

                        if (sp1[0].StartsWith("234 REVIEW") || sp1[0].StartsWith("234  REVIEW"))
                        {
                            string[] sp2 = sp1[1].Split('|');

                            if (sp2.Length == 3)
                                fulldesc += sp2[2];
                            else
                            {
                                for (int i = 2; i < sp2.Length; i++)
                                    fulldesc += "|" + sp2[i];
                            }


                            maxpart = int.Parse(sp2[1]);
                        }
                        multipart = true;
                        if (part == maxpart)
                        {
                            decodedstring = sp1[0] + "\n0|1|" + fulldesc + "\n";
                            multipart = false;
                        }
                    }
                }
            } while (multipart && !errorOccurred);

            if (errorOccurred)
            {
                socketResponse = string.Empty;
            }
            else
            {
                // there should be 2 newline characters in each response
                // the first is after the command .e.g "220 FILE"
                // the second is at the end of the data
                int i = 0, ipos = 0, foundpos = 0;
                foreach (char c in decodedstring)
                {
                    if (c == '\n')
                    {
                        //MyAnimeLog.Write("NEWLINE FOUND AT: {0}", ipos);
                        i++;
                        foundpos = ipos;
                    }
                    ipos++;
                }

                if (i != 2)
                {
                    socketResponse = decodedstring;

                    TimeSpan ts = DateTime.Now - start;
                    string msg = string.Format("UDP_RESPONSE in {0} ms - {1} ", ts.TotalMilliseconds, socketResponse);
                    JMMService.LogToSystem(Constants.DBLogType.APIAniDBUDP, msg);
                }
                else
                {
                    socketResponse = decodedstring.Substring(0, foundpos + 1);

                    TimeSpan ts = DateTime.Now - start;
                    string msg = string.Format("UDP_RESPONSE_TRUNC in {0}ms - {1} ", ts.TotalMilliseconds,
                        socketResponse);
                    JMMService.LogToSystem(Constants.DBLogType.APIAniDBUDP, msg);
                }
            }
            int val = 0;
            if (socketResponse.Length > 2)
                int.TryParse(socketResponse.Substring(0, 3), out val);
            this.ResponseCode = val;

            // if we get banned pause the command processor for a while
            // so we don't make the ban worse
            if (ResponseCode == 555)
            {
                JMMService.AnidbProcessor.IsBanned = true;
                JMMService.AnidbProcessor.BanOrigin = "UDP";
            }
            else
            {
                JMMService.AnidbProcessor.IsBanned = false;
                JMMService.AnidbProcessor.BanOrigin = "";
            }

            // 598 UNKNOWN COMMAND usually means we had connections issue
            // 506 INVALID SESSION
            // 505 ILLEGAL INPUT OR ACCESS DENIED
            // reset login status to start again
            if (ResponseCode == 598 || ResponseCode == 506 || ResponseCode == 506)
            {
                JMMService.AnidbProcessor.IsInvalidSession = true;
                logger.Trace("FORCING Logout because of invalid session");
                ForceReconnection();
            }

            // 600 INTERNAL SERVER ERROR
            // 601 ANIDB OUT OF SERVICE - TRY AGAIN LATER
            // 602 SERVER BUSY - TRY AGAIN LATER
            // 604 TIMEOUT - DELAY AND RESUBMIT
            if (ResponseCode == 600 || ResponseCode == 601 || ResponseCode == 602 || ResponseCode == 604)
            {
                string errormsg = "";
                switch (ResponseCode)
                {
                    case 600:
                        errormsg = "600 INTERNAL SERVER ERROR";
                        break;
                    case 601:
                        errormsg = "601 ANIDB OUT OF SERVICE - TRY AGAIN LATER";
                        break;
                    case 602:
                        errormsg = "602 SERVER BUSY - TRY AGAIN LATER";
                        break;
                    case 604:
                        errormsg = "TIMEOUT - DELAY AND RESUBMIT";
                        break;
                }
                logger.Trace("FORCING Logout because of invalid session");
                JMMService.AnidbProcessor.ExtendPause(300, errormsg);
            }
        }

        public AniDBUDPCommand()
        {
            ResponseCode = 0;
        }

        public void ForceReconnection()
        {
            try
            {
                if (JMMService.AnidbProcessor != null)
                {
                    logger.Info("Forcing reconnection to AniDB");
                    JMMService.AnidbProcessor.Dispose();
                    Thread.Sleep(1000);

                    JMMService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                        ServerSettings.AniDB_ServerAddress,
                        ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }
    }
}