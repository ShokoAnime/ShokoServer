using System;
using JMMContracts;

namespace JMMServer.Providers.Azure
{
    public class AdminMessage
    {
        public int AdminMessageId { get; set; }

        public long MessageDate { get; set; }
        public int MessageType { get; set; }
        public string Message { get; set; }
        public string MessageURL { get; set; }
        public string Self { get; set; }

        public DateTime MessageDateAsDate
        {
            get { return TimeZone.CurrentTimeZone.ToLocalTime(Utils.GetAniDBDateAsDate((int)MessageDate).Value); }
        }

        public bool HasMessageURL
        {
            get { return !string.IsNullOrEmpty(MessageURL); }
        }

        public override string ToString()
        {
            return string.Format("{0} - {1} - {2}", AdminMessageId, MessageDateAsDate, Message);
        }

        public Contract_AdminMessage ToContract()
        {
            var contract = new Contract_AdminMessage();

            contract.AdminMessageId = AdminMessageId;
            contract.MessageDate = MessageDate;
            contract.MessageType = MessageType;
            contract.Message = Message;
            contract.MessageURL = MessageURL;


            return contract;
        }
    }
}