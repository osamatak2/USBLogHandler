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

namespace SelectFileToUsb
{
    public partial class MainWindow : Window
    {
        // USBに書き込むべきファイルのリスト
        private List<string> LvFilePaths;
        // USBに書き込むべきファイルの名前のコレクション  
        public ObservableCollection<string> LvFileNames { get; set; }

        public MainWindow()
        {
            // 排他制御
            if(Directory.GetFiles(@"..\..\..\semaphore").Count() > 0){
                MessageBox.Show("他で使用中です。しばらくしてから起動してください");
                Close();
            }

            // 排他制御できるように、自らのユーザー名を付けたファイルを作成して保存しておく
            string spName = @"..\..\..\semaphore\" + Environment.UserName + ".txt";            
            var fs = File.Create(spName);
            fs.Close();
                        
            LvFileNames = new ObservableCollection<string>();
            LvFilePaths = new List<string>();

            // （もしなければ）データベース「myDb.db」、テーブル「request」を作成する
            var connectionStringBuilder = new SQLiteConnectionStringBuilder();
            connectionStringBuilder.DataSource = @"..\..\..\sqlite_db\myDb.db";
            using (var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString))
            {
               connection.Open();
               using(var tableCmd = connection.CreateCommand())
               {
                    tableCmd.CommandText = "CREATE TABLE IF NOT EXISTS request(" +
                            "id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                            "date TEXT NOT NULL," +
                            "guid TEXT NOT NULL," +
                            "user TEXT NOT NULL," +
                            "is_written TEXT NOT NULL)";
                    tableCmd.ExecuteNonQuery();
               }
            }

            InitializeComponent();
        }

        // 「選択する」ボタンのハンドラ
        private void OpenfileButton_Click(object sender, RoutedEventArgs e)
        {
            // ファイル選択ダイアログを開く
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;           
            openFileDialog.InitialDirectory = @"c:\";
            if (openFileDialog.ShowDialog() == true)
            {
                string[] filePaths = openFileDialog.FileNames;

                // 選択されたファイルから、ファイル名とパスを取得し、
                // それぞれ、LvFileNamesとLvFilePathsに格納する
                foreach (string filePath in filePaths)
                {
                    string fileName = Path.GetFileName(filePath);
                    if (!LvFileNames.Contains(fileName))
                    {
                        LvFileNames.Add(fileName);
                        LvFilePaths.Add(filePath);
                    }
                }
            }

            // 「クリア」ボタン・「確定する」ボタン　を活性化する
            // 「選択する」ボタンは、「追加する」ボタンに名称を変更する
            if (LvFileNames.Count > 0)
            {
                ClearButton.IsEnabled = true;
                ConfirmButton.IsEnabled = true;
                OpenfileButton.Content = "追加する";
            }
        }

        // 「クリア」ボタンのハンドラ
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // LvFileNamesとLvFilePathsについて、それぞれ全ての要素を削除する
            int count = LvFileNames.Count;
            while (count > 0)
            {
                LvFileNames.RemoveAt(count -1);
                LvFilePaths.RemoveAt(count -1);
                count--;
            }
            // 「クリア」ボタン・「確定する」ボタン　を不活性化する
            // 「追加する」ボタンは、「選択する」ボタンに名称を戻す
            ClearButton.IsEnabled = false;
            ConfirmButton.IsEnabled = false;
            OpenfileButton.Content = "選択する";
        }

        // 「確定する」ボタンのハンドラ
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (LvFileNames.Count > 0)
            {
                // Logクラスのインスタンスを作成する
                string guid = Guid.NewGuid().ToString();
                Log log = new Log(
                    DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
                    guid,
                    Environment.UserName
                    );

                // データベースにレコードを追加するカスタム関数「InsertData」を呼ぶ
                if(InsertData(log))
                {
                    // tempフォルダ内に、ユニーク名付きフォルダを作成する
                    string dirPath = Path.GetFullPath(Environment.CurrentDirectory)
                        + @"\..\..\..\temp\" + guid + @"\";
                    Directory.CreateDirectory(dirPath);

                    // tempフォルダ内に、選択されたファイルをコピーする
                    foreach (string path in LvFilePaths)
                    {
                        File.Copy(path, dirPath + Path.GetFileName(path));
                    }
                    
                    // 「追加する」ボタン・「クリア」ボタン・「確定する」ボタン　を不活性化する
                    OpenfileButton.IsEnabled = false;
                    ClearButton.IsEnabled = false;
                    ConfirmButton.IsEnabled = false;
                    MessageBox.Show("確定しました。終了してください");
                }
            }
            else
            {
                MessageBox.Show("ファイルが選択されていません");
            }
        }

        // 「終了」ボタンのハンドラ
        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // データベースにレコードを追加する関数
        private bool InsertData(Log log)
        {
            var connectionStringBuilder = new SQLiteConnectionStringBuilder();

            // データベースのパスを指定する
            connectionStringBuilder.DataSource = @"..\..\..\sqlite_db\myDb.db";

            // SQLを実行するコマンドとして、INSERT文を作成する
            string insertCmdText = "INSERT INTO request(date, guid, user, is_written) "
                + "VALUES('" + log.Date + "','" + log.Guid + "','" + log.User + "','notyet')";

            // データベースに接続してコマンドを実行する
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
                            SQLiteCommand insertCmd = connection.CreateCommand();
                            insertCmd.CommandText = insertCmdText;
                            insertCmd.ExecuteNonQuery();
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
    }
}

