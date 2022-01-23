namespace GetReport
{
    public class Record
    {
        public int WriterId { get; set; }
        public string WriterDate { get; set; }
        public string Writer { get; set; }
        public string UserDate { get; set; }
        public string User { get; set; }
        public string Guid { get; set; }
        public int DetailsId { get; set; }
        public string FileName { get; set; }
        public static string Header 
            = "WriterId, WriterDate, Writer, UserDate, User, Guid, DetailsId, FileName";

        public Record(int writerId, string writerDate, string writer, string userDate, 
            string user, string guid, int detailsId, string fileName)
        {
            WriterId   = writerId;
            WriterDate = writerDate;
            Writer     = writer;
            UserDate   = userDate;
            User       = user;
            Guid       = guid;
            DetailsId  = detailsId;
            FileName   = fileName;
        }

        public string GetString()
        {
            string str = 
                this.WriterId.ToString() + ", " + '"' + 
                this.WriterDate + '"' + ", " +
                this.Writer + ", " + '"' +
                this.UserDate + '"' + ", " +
                this.User + ", " +
                this.Guid + ", " +
                this.DetailsId.ToString() + ", " + '"' + 
                this.FileName + '"';
            return str;
        }
    }
}
