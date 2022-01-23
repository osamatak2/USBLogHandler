using Microsoft.Win32;
using System;
using System.IO;
// using System.IO.Compression;
// using System.IO.Compression.FileSystem;
using Path = System.IO.Path;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Data.SQLite;

namespace WriteToUsb
{
    public partial class MainWindow : Window
    {

        // USBに書き込むべきファイルの名前のコレクション  
        public ObservableCollection<Log> LvLogs;
        public ObservableCollection<string> LbFiles;

        public MainWindow()
        {
            LvLogs = new ObservableCollection<Log>();
            LbFiles = new ObservableCollection<string>();

            // データベース に接続する
            var connectionStringBuilder = new SQLiteConnectionStringBuilder();
            connectionStringBuilder.DataSource = @"..\..\..\sqlite_db\myDb.db";
            
            try
            {
                using (var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString))
                {
                    connection.Open();
                    using(var command = connection.CreateCommand())
                    {
                        // まだUSB書き出しが済でないレコードを抽出するSQL文
                        command.CommandText = "SELECT * FROM request WHERE is_written <> 'done'";
                        var reader = command.ExecuteReader();

                        if (reader.HasRows)
                        {
                            // 各レコードから、必要なカラムの要素だけでLogクラスのインスタンスを作る
                            while (reader.Read())
                            {
                                string date = Convert.ToString(reader["date"]);
                                string user = Convert.ToString(reader["user"]);
                                string guid = Convert.ToString(reader["guid"]);
                                Log log = new Log(date, guid, user);
                                LvLogs.Add(log);
                            }
                        }
                        else
                        {
                            MessageBox.Show("(注意) 対象のデータがありません");
                        }
                        reader.Close();
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
                MessageBox.Show("データベースへの接続または読み込みできませんでした。再度起動してください。");
                Application.Current.Shutdown();
            }

            InitializeComponent();
        }

        // コレクションLvLogsをListViewにデータバインディング
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ListView.DataContext = LvLogs;
            ListBox.DataContext = LbFiles;
        }
        
        // ListViewのイベントハンドラ
        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // LbFilesの要素を全て削除する
            int count = LbFiles.Count;
            while (count > 0)
            {
                LbFiles.RemoveAt(count - 1);
                count--;
            }

            var item = (Log)ListView.SelectedItem;
            if (item == null)
            {
                MessageBox.Show("そのレコードは存在しません。いったん終了してツールをもう一度起動してください");
                return;
            }

            string[] files = GetSelectedFiles(item);
            if (files == null)
            {
                MessageBox.Show("対象のファイルが存在しません\r\n" +
                    "削除されたか、既に承認済です。確認してください\r\n" + 
                    "いったん終了してツールをもう一度起動してください");
                return;
            }

            foreach(string file in files)
            {
                LbFiles.Add(Path.GetFileName(file));
            }
            // 「実行する」ボタン　を活性化する
            WriteButton.IsEnabled = true;
        }

        // 「実行する」ボタン
        private void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ListView.SelectedItem == null)
            {
                MessageBox.Show("レコードが選択されていないか、レコードが存在しません");
                return;
            }
            var item = (Log)ListView.SelectedItem;            
            
            // 選択したァイルのパスを取得
            string[] files = GetSelectedFiles(item);
            if (files == null)
            {
                MessageBox.Show("対象のファイルが存在しません\r\n" +
                    "削除されたか、既に承認済です。確認してください\r\n" + 
                    "いったん終了してツールをもう一度起動してください");
                return;
            }

            // データベースに記録する
            //var result = writeDb(files, writeDateTime);
            string writeDateTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

            if(writeDb(files, writeDateTime))
            {
                // USBに書き込みし、書き込み日時を取得する
                writeFilesToUsb(files);

                // ZIPファイルを作成して保存する(ZIP化は未実装。4.0では使えない)
                createZip(item,files); 

                // 「実行する」ボタンを無効化
                WriteButton.IsEnabled = false;

                // ListView, ListBox を無効化
                ListView.IsEnabled = false;
                ListBox.IsEnabled = false;

                MessageBox.Show("・USBメモリに出力しました\r\n" +
                    "・archiveフォルダにファイルを保存しました\r\n" +
                    "・データべースに記録しました\r\n\r\n  >>>  処理が完了しました。終了してください");
            }
        }

        // 「終了」ボタン
        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
           Application.Current.Shutdown();
        }

        // ListBoxに表示されているファイルを取得する
        private string[] GetSelectedFiles(Log logItem)
        {
            string dirPath = @"..\..\..\temp\" + logItem.Guid;
            if (!Directory.Exists(dirPath))
            {
                return null;
            }
            
            string[] files = Directory.GetFiles(dirPath, "*");
            return files;
        }

        private void writeFilesToUsb(string[] filePaths)
        {
            // （暫定処置）USBメモリ代わりのダミーのフォルダ
            string dummyDir = @"..\..\..\dummyUSB\";

            // copy files
            foreach (string path in filePaths)
               {
                   File.Copy(path, dummyDir + Path.GetFileName(path), true);  //@"D:\"
               }
        }

        // テーブルWriteLogToUsb、fileDetailsを作成し、記録するメソッド
        private bool writeDb(string[] filePaths, string datetime)
        {
            var connectionStringBuilder = new SQLiteConnectionStringBuilder();
            connectionStringBuilder.DataSource = @"..\..\..\sqlite_db\myDb.db";

            // writeLogテーブルを作成するSQL文
            string writeLogTblText = "CREATE TABLE IF NOT EXISTS writeLog(" +
                            "id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                            "write_date TEXT NOT NULL," +
                            "writer TEXT NOT NULL," +
                            "user_date TEXT NOT NULL," +
                            "user TEXT NOT NULL," +
                            "guid TEXT NOT NULL)";
            
            // writeLogテーブルにUSBへの書き込みログをINSERTするSQL文
            var log = (Log)ListView.SelectedItem;
            string insertLogCmdText = "INSERT INTO writeLog"
                + "(write_date, writer, user_date, user, guid) "
                + "VALUES('" + datetime + "','" + Environment.UserName + "','" + log.Date + "','" + log.User + "','" + log.Guid + "')";
            
            // fileDetailsテーブルを作成するSQL文
            string fileDetailsTblText = "CREATE TABLE IF NOT EXISTS fileDetails(" +
                            "id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                            "guid TEXT NOT NULL," +
                            "file_name TEXT NOT NULL)";

            // requestテーブルの該当レコードに、USB書出し済である旨を記録sするSQL文
            string updateIsWrittenText = 
                "UPDATE request SET is_written = 'done' WHERE guid = '" + log.Guid + "'";
            
            // データベースに接続する
            try
            {
                using (var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString))
                {
                    connection.Open();

                    // トランザクションとして実行する
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            SQLiteCommand command = connection.CreateCommand();
                            
                            command.CommandText = writeLogTblText;
                            command.ExecuteNonQuery();

                            command.CommandText = insertLogCmdText;
                            command.ExecuteNonQuery();

                            command.CommandText = fileDetailsTblText;
                            command.ExecuteNonQuery();

                            command.CommandText = updateIsWrittenText;
                            command.ExecuteNonQuery();

                            foreach(string fileName in filePaths)
                            {
                                // fileDetailsテーブルにファイル明細をINSERTするSQL文
                                string insertDetailsCmdText = "INSERT INTO fileDetails"
                                    + "(guid, file_name) "
                                    + "VALUES('" + log.Guid + "','" + Path.GetFileName(fileName) + "')";
                                command.CommandText = insertDetailsCmdText;
                                command.ExecuteNonQuery();
                            }
                            transaction.Commit();
                        }
                        catch (System.Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show(ex.Message);
                            MessageBox.Show("データベースに書込みできませんでした。再度確定するか、" +
                                "終了してしばらく経ってからやり直してください。");
                            return false;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
            return true;
        }

        private void createZip(Log zipItem, string[] srcFiles)
        {
            // まだZIPには対応していない。現在の環境では.NET FRAMEWORK4.0のアセンブリしかないため。
            string archiveDir = @"..\..\..\archive\" + zipItem.Guid;
            Directory.CreateDirectory(archiveDir);
            foreach (string path in srcFiles)
               {
                   File.Copy(path, archiveDir + @"\" + Path.GetFileName(path), true);
               }
            

            // string dstPath = @"..\..\..\archive\" + zipItem.Guid + ".zip";
            // if (File.Exists(dstPath) == true) File.Delete(dstPath);

            // using(FileStream fs = new FileStream(dstPath, FileMode.CreateNew))
            // {
            //     using(ZipArchive za = new ZipArchive(fs, ZipArchiveMode.Create))
            //     {
            //         foreach (string srcFile in srcFiles)
            //         {
            //             za.CreateEntryFromFile(srcFile, Path.GetFileName(srcFile));
            //         }
            //     }
                
            // }

            //tempディレクトリ内の該当ディレクトリを削除する
            string deleteDir = @"..\..\..\temp\" + zipItem.Guid;
            DeleteDirectory(deleteDir);
        }

        private void DeleteDirectory(string dir)
        {
            DirectoryInfo di = new DirectoryInfo(dir);

            //フォルダ以下のすべてのファイル、フォルダの属性を削除する
            RemoveReadonlyAttribute(di);

            di.Delete(true);
        }

        private void RemoveReadonlyAttribute(DirectoryInfo dirInfo)
        {
            //フォルダの属性を変更する
            if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                dirInfo.Attributes = FileAttributes.Normal;
            }

            //フォルダ内のすべてのファイルの属性を変更する
            foreach (FileInfo fi in dirInfo.GetFiles())
            {
                if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    fi.Attributes = FileAttributes.Normal;
            }

            //サブフォルダの属性を再帰的に変更する
            foreach (DirectoryInfo di in dirInfo.GetDirectories())
            {
                RemoveReadonlyAttribute(di);
            }
        }
    }
}

