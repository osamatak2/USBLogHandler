namespace SelectFileToUsb
{
    public class Log
    {
        public string Date { get; set; }
        public string Guid { get; set; }
        public string User { get; set; }

        public Log(string date, string guid, string user)
        {
            Date = date;
            Guid = guid;
            User = user;
        }
    }
}
