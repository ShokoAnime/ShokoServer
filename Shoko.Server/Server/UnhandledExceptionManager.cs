using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using NLog;
using Sentry;
using Shoko.Server.Utilities;

namespace Shoko.Server.Server;

public sealed class UnhandledExceptionManager
{
    private static Assembly _objParentAssembly;
    private static string _strException;

    private static Assembly ParentAssembly()
    {
        return _objParentAssembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
    }

    //--
    //-- This *MUST* be called early in your application to set up global error handling
    //--
    public static void AddHandler()
    {
        //-- attempt to load optional settings from .config file
        //LoadConfigSettings();

        //-- we don't need an unhandled exception handler if we are running inside
        //-- the vs.net IDE; it is our "unhandled exception handler" in that case
        //if (Variables.AppSettings.IgnoreDebugErrors)
        if (Debugger.IsAttached)
        {
            return;
        }

        //-- track the parent assembly that set up error handling
        //-- need to call this NOW so we set it appropriately; otherwise
        //-- we may get the wrong assembly at exception time!
        ParentAssembly();

        //-- for console applications
        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

        //-- I cannot find a good way to programatically detect a console app, so that must be specified.
        //_blnConsoleApp = blnConsoleApp;
    }

    //--
    //-- handles Application.ThreadException event
    //--
    public static void ThreadExceptionHandler(object sender, ThreadExceptionEventArgs e)
    {
        GenericExceptionHandler(e.Exception);
    }

    //--
    //-- handles AppDomain.CurrentDoamin.UnhandledException event
    //--
    private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
    {
        var objException = (Exception)args.ExceptionObject;
        GenericExceptionHandler(objException);
    }

    //--
    //-- exception-safe file attrib retrieval; we don't care if this fails
    //--
    private static DateTime AssemblyFileTime(Assembly objAssembly)
    {
        try
        {
            return File.GetLastWriteTime(objAssembly.Location);
        }
        catch
        {
            return DateTime.MaxValue;
        }
    }

    //--
    //-- returns build datetime of assembly
    //-- assumes default assembly value in AssemblyInfo:
    //-- <Assembly: AssemblyVersion("1.0.*")>
    //--
    //-- filesystem create time is used, if revision and build were overridden by user
    //--
    private static DateTime AssemblyBuildDate(Assembly objAssembly, bool blnForceFileDate = false)
    {
        var objVersion = objAssembly.GetName().Version;
        DateTime dtBuild = default;

        if (blnForceFileDate)
        {
            dtBuild = AssemblyFileTime(objAssembly);
        }
        else
        {
            //dtBuild = ((DateTime)"01/01/2000").AddDays(objVersion.Build).AddSeconds(objVersion.Revision * 2);
            dtBuild =
                Convert.ToDateTime("01/01/2000")
                    .AddDays(objVersion.Build)
                    .AddSeconds(objVersion.Revision * 2);
            if (TimeZone.IsDaylightSavingTime(DateTime.Now,
                    TimeZone.CurrentTimeZone.GetDaylightChanges(DateTime.Now.Year)))
            {
                dtBuild = dtBuild.AddHours(1);
            }

            if ((dtBuild > DateTime.Now) | (objVersion.Build < 730) | (objVersion.Revision == 0))
            {
                dtBuild = AssemblyFileTime(objAssembly);
            }
        }

        return dtBuild;
    }

    //--
    //-- turns a single stack frame object into an informative string
    //--
    private static string StackFrameToString(StackFrame sf)
    {
        var sb = new StringBuilder();
        var intParam = 0;
        MemberInfo mi = sf.GetMethod();

        var _with1 = sb;
        //-- build method name
        _with1.Append("   ");
        _with1.Append(mi.DeclaringType.Namespace);
        _with1.Append(".");
        _with1.Append(mi.DeclaringType.Name);
        _with1.Append(".");
        _with1.Append(mi.Name);

        //-- build method params
        var objParameters = sf.GetMethod().GetParameters();
        ParameterInfo objParameter = null;
        _with1.Append("(");
        intParam = 0;
        foreach (var objParameter_loopVariable in objParameters)
        {
            objParameter = objParameter_loopVariable;
            intParam += 1;
            if (intParam > 1)
            {
                _with1.Append(", ");
            }

            _with1.Append(objParameter.Name);
            _with1.Append(" As ");
            _with1.Append(objParameter.ParameterType.Name);
        }

        _with1.Append(")");
        _with1.Append(Environment.NewLine);

        //-- if source code is available, append location info
        _with1.Append("       ");
        if (sf.GetFileName() == null || sf.GetFileName().Length == 0)
        {
            _with1.Append(Path.GetFileName(ParentAssembly().CodeBase));
            //-- native code offset is always available
            _with1.Append(": N ");
            _with1.Append(string.Format("{0:#00000}", sf.GetNativeOffset()));
        }
        else
        {
            _with1.Append(Path.GetFileName(sf.GetFileName()));
            _with1.Append(": line ");
            _with1.Append(string.Format("{0:#0000}", sf.GetFileLineNumber()));
            _with1.Append(", col ");
            _with1.Append(string.Format("{0:#00}", sf.GetFileColumnNumber()));
            //-- if IL is available, append IL location info
            if (sf.GetILOffset() != StackFrame.OFFSET_UNKNOWN)
            {
                _with1.Append(", IL ");
                _with1.Append(string.Format("{0:#0000}", sf.GetILOffset()));
            }
        }

        _with1.Append(Environment.NewLine);
        return sb.ToString();
    }

    //--
    //-- enhanced stack trace generator
    //--
    private static string EnhancedStackTrace(StackTrace objStackTrace, string strSkipClassName = "")
    {
        var intFrame = 0;

        var sb = new StringBuilder();

        sb.Append(Environment.NewLine);
        sb.Append("---- Stack Trace ----");
        sb.Append(Environment.NewLine);

        for (intFrame = 0; intFrame <= objStackTrace.FrameCount - 1; intFrame++)
        {
            var sf = objStackTrace.GetFrame(intFrame);
            MemberInfo mi = sf.GetMethod();

            if (!string.IsNullOrEmpty(strSkipClassName) && mi.DeclaringType.Name.IndexOf(strSkipClassName) > -1)
            {
                //-- don't include frames with this name
            }
            else
            {
                sb.Append(StackFrameToString(sf));
            }
        }

        sb.Append(Environment.NewLine);

        return sb.ToString();
    }

    //--
    //-- enhanced stack trace generator (exception)
    //--
    private static string EnhancedStackTrace(Exception objException)
    {
        var objStackTrace = new StackTrace(objException, true);
        return EnhancedStackTrace(objStackTrace);
    }

    //--
    //-- enhanced stack trace generator (no params)
    //--
    private static string EnhancedStackTrace()
    {
        var objStackTrace = new StackTrace(true);
        return EnhancedStackTrace(objStackTrace, "ExceptionManager");
    }

    //--
    //-- generic exception handler; the various specific handlers all call into this sub
    //--

    private static void GenericExceptionHandler(Exception objException)
    {
        try
        {
            Analytics.PostException(objException, true);
            SentrySdk.CaptureException(objException);
        }
        catch
        {
            // ignore
        }

        //-- turn the exception into an informative string
        try
        {
            _strException = ExceptionToString(objException);
        }
        catch (Exception ex)
        {
            _strException = "Error '" + ex.Message + "' while generating exception string";
        }

        //-- log this error to various locations
        try
        {
            //-- textfile logging takes < 50ms
            ExceptionToFile();
        }
        catch
        {
            //-- generic catch because any exceptions inside the UEH
            //-- will cause the code to terminate immediately
        }

        //if (Variables.AppSettings.KillAppOnException)
        //{
        //As far as the email not being send when a user closes the dialog too fast, 
        //I see that you were threading off the email to speed display of the exception 
        //dialog. Well, if the user clicks ok before the SMTP can be sent, the thread 
        //is killed immediately and the email will never make it out. To fix that, I changed 
        //the scope of the objThread to class level and right before the KillApp() call I 
        //do a objThread.Join(new TimeSpan(0, 0, 30)) to wait up to 30 seconds for it's 
        //completion. Now the email is sent reliably. Changing the scope of the objThread 
        //mandated that the class not be static (Shared) anymore, so I changed the relevant 
        //functions and I instantiate an object of the exception handler to AddHandler() with.

        //objThread.Join(new TimeSpan(0, 0, 30));	// to wait 30 seconds for completion
        //}
    }

    //--
    //-- write an exception to a text file
    //--
    private static void ExceptionToFile()
    {
        LogManager.GetCurrentClassLogger().Fatal(_strException);
    }

    //--
    //-- exception-safe "domain\username" retrieval from Environment
    //--
    private static string CurrentEnvironmentIdentity()
    {
        try
        {
            return Environment.UserDomainName + "\\" + Environment.UserName;
        }
        catch
        {
            return string.Empty;
        }
    }

    //--
    //-- retrieve identity with fallback on error to safer method
    //--
    private static string UserIdentity()
    {
        var strTemp = CurrentEnvironmentIdentity();
        return strTemp;
    }

    //--
    //-- gather some system information that is helpful to diagnosing
    //-- exception
    //--
    internal static string SysInfoToString(bool blnIncludeStackTrace = false)
    {
        var objStringBuilder = new StringBuilder();

        var _with4 = objStringBuilder;

        _with4.Append("Date and Time:         ");
        _with4.Append(DateTime.Now);
        _with4.Append(Environment.NewLine);

        _with4.Append("Machine Name:          ");
        try
        {
            _with4.Append(Environment.MachineName);
        }
        catch (Exception e)
        {
            _with4.Append(e.Message);
        }

        _with4.Append(Environment.NewLine);

        _with4.Append("IP Address:            ");
        _with4.Append(GetCurrentIP());
        _with4.Append(Environment.NewLine);

        _with4.Append("Current User:          ");
        _with4.Append(UserIdentity());
        _with4.Append(Environment.NewLine);
        _with4.Append(Environment.NewLine);

        _with4.Append("Application Domain:    ");
        try
        {
            //_with4.Append(System.AppDomain.CurrentDomain.FriendlyName());
            _with4.Append(AppDomain.CurrentDomain.FriendlyName);
        }
        catch (Exception e)
        {
            _with4.Append(e.Message);
        }

        _with4.Append(Environment.NewLine);
        _with4.Append("Assembly Codebase:     ");
        try
        {
            //_with4.Append(ParentAssembly().CodeBase());
            _with4.Append(ParentAssembly().CodeBase);
        }
        catch (Exception e)
        {
            _with4.Append(e.Message);
        }

        _with4.Append(Environment.NewLine);

        _with4.Append("Assembly Full Name:    ");
        try
        {
            _with4.Append(ParentAssembly().FullName);
        }
        catch (Exception e)
        {
            _with4.Append(e.Message);
        }

        _with4.Append(Environment.NewLine);

        _with4.Append("Assembly Version:      ");
        try
        {
            //_with4.Append(ParentAssembly().GetName().Version().ToString);
            _with4.Append(ParentAssembly().GetName().Version);
        }
        catch (Exception e)
        {
            _with4.Append(e.Message);
        }

        _with4.Append(Environment.NewLine);

        _with4.Append("Assembly Build Date:   ");
        try
        {
            _with4.Append(AssemblyBuildDate(ParentAssembly()).ToString());
        }
        catch (Exception e)
        {
            _with4.Append(e.Message);
        }

        _with4.Append(Environment.NewLine);
        _with4.Append(Environment.NewLine);

        if (blnIncludeStackTrace)
        {
            _with4.Append(EnhancedStackTrace());
        }

        return objStringBuilder.ToString();
    }

    //--
    //-- translate exception object to string, with additional system info
    //--
    internal static string ExceptionToString(Exception objException)
    {
        var objStringBuilder = new StringBuilder();

        if (objException.InnerException != null)
        {
            //-- sometimes the original exception is wrapped in a more relevant outer exception
            //-- the detail exception is the "inner" exception
            //-- see http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dnbda/html/exceptdotnet.asp
            var _with5 = objStringBuilder;
            _with5.Append("(Inner Exception)");
            _with5.Append(Environment.NewLine);
            _with5.Append(ExceptionToString(objException.InnerException));
            _with5.Append(Environment.NewLine);
            _with5.Append("(Outer Exception)");
            _with5.Append(Environment.NewLine);
        }

        var _with6 = objStringBuilder;
        //-- get general system and app information
        _with6.Append(SysInfoToString());

        //-- get exception-specific information
        _with6.Append("Exception Source:      ");
        try
        {
            _with6.Append(objException.Source);
        }
        catch (Exception e)
        {
            _with6.Append(e.Message);
        }

        _with6.Append(Environment.NewLine);

        _with6.Append("Exception Type:        ");
        try
        {
            _with6.Append(objException.GetType().FullName);
        }
        catch (Exception e)
        {
            _with6.Append(e.Message);
        }

        _with6.Append(Environment.NewLine);

        _with6.Append("Exception Message:     ");
        /*try
        {
            //_with6.Append(ret.Result);
            Task.Factory.StartNew<string>(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
                return objException.Message;
            })
                .ContinueWith(ret =>
                {
                    _with6.Append(ret.Result);
                })
                .Wait();
        }
        catch (Exception e)
        {
            _with6.Append(e.Message);
        }*/
        _with6.Append(Environment.NewLine);

        _with6.Append("Exception Target Site: ");
        try
        {
            _with6.Append(objException.TargetSite.Name);
        }
        catch (Exception e)
        {
            _with6.Append(e.Message);
        }

        _with6.Append(Environment.NewLine);

        try
        {
            var x = EnhancedStackTrace(objException);
            _with6.Append(x);
        }
        catch (Exception e)
        {
            _with6.Append(e.Message);
        }

        _with6.Append(Environment.NewLine);

        return objStringBuilder.ToString();
    }

    //--
    //-- get IP address of this machine
    //-- not an ideal method for a number of reasons (guess why!)
    //-- but the alternatives are very ugly
    //--
    private static string GetCurrentIP()
    {
        try
        {
            var strIP = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
            return strIP;
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}
