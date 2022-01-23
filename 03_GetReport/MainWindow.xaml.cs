using Microsoft.Win32;
using System;
using System.IO;
using Path = System.IO.Path;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Data.SQLite;
 
namespace GetReport
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Record> Records = new ObservableCollection<Record>();

        public List<String> wrDateList;
        public List<String> writerList;
        public List<String> userList;

        public MainWindow()
        {
            // データベース に接続する
            var connectionStringBuilder = new SQLiteConnectionStringBuilder();
            connectionStringBuilder.DataSource = @"..\..\..\sqlite_db\myDb.db";

            string createViewText = 
                "create view if not exists " + 
                    "view_join as " + 
                    "select writeLog.id, write_date, " + 
                        "writer, user_date, user, writeLog.guid, " + 
                        "fileDetails.id, fileDetails.file_name " + 
                    "from writeLog inner join fileDetails on writeLog.guid = fileDetails.guid";
            
            string viewText = "select * from view_join";

            try
            {
                using (var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString))
                {
                    connection.Open();
                    using(SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            SQLiteCommand command = connection.CreateCommand();
                            command.CommandText = createViewText;
                            command.ExecuteNonQuery();

                            command.CommandText = viewText;

                            var reader = command.ExecuteReader();
                            while (reader.Read())
                            {
                                int writerId      = System.Convert.ToInt32(reader["id"]);
                                string writerDate = Convert.ToString(reader["write_date"]);
                                string writer     = Convert.ToString(reader["writer"]);
                                string userDate   = Convert.ToString(reader["user_date"]);
                                string user       = Convert.ToString(reader["user"]);
                                string guid       = Convert.ToString(reader["guid"]);
                                int detailsId     = System.Convert.ToInt32(reader["id:1"]);
                                string fileName   = Convert.ToString(reader["file_name"]);
                                var record = new Record(writerId, writerDate, writer, userDate, 
                                    user, guid, detailsId, fileName);
                                Records.Add(record);
                            }

                            wrDateList = Records.Select(x => x.WriterDate).Distinct().ToList();
                            wrDateList.Insert(0," ");

                            writerList = Records.Select(x => x.Writer).Distinct().ToList();
                            writerList.Insert(0," ");

                            userList = Records.Select(x => x.User).Distinct().ToList();
                            userList.Insert(0," ");

                            transaction.Commit();
                        }
                        catch (System.Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show(ex.Message);
                            MessageBox.Show("データベースを読み込みできませんでした。\r\n" +
                                "終了してしばらく経ってからやり直してください。");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
                MessageBox.Show("データベースに接続できませんでした。\r\n" +
                                "ヘルプデスクに連絡してください。");
                Application.Current.Shutdown();;
            }

            InitializeComponent();

            ComboBox1.DataContext = wrDateList;
            ComboBox2.DataContext = writerList;
            ComboBox3.DataContext = userList;

            ListViewMain.DataContext = Records;
        }

        // フィルタが選択された、または、選択が外された場合のハンドラ
        // 各フィルタに応じて、コレクションをフィルタし直している
        private void ComboBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item1 = (string)ComboBox1.SelectedItem;
            var item2 = (string)ComboBox2.SelectedItem;
            var item3 = (string)ComboBox3.SelectedItem;
            var filterList = Records.Select(x => x);

            if(ComboBox1.SelectedIndex != 0)
            {
                filterList = filterList.Where(x => x.WriterDate == item1);
            }
            if(ComboBox2.SelectedIndex != 0)
            {
                filterList = filterList.Where(x => x.Writer == item2);
            }
            if(ComboBox3.SelectedIndex != 0)
            {
                filterList = filterList.Where(x => x.User == item3);
            }

            ListViewMain.DataContext = filterList;
        }
        
        // エクスポートボタンのハンドラ
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            string destPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                + @"\Downloads\usb_log.csv";
            using (var sw = new StreamWriter(destPath, false, Encoding.GetEncoding("Shift_JIS")))
            {
                sw.WriteLine(Record.Header);
                foreach (var record in Records)
                {
                    sw.WriteLine(record.GetString());
                }
            }
            MessageBox.Show("ダウンロードフォルダにエクスポートしました");
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
           Application.Current.Shutdown();
        }
    }
}