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

            // データベースを作成する
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
        // 「選択する」ボタン
        private void OpenfileButton_Click(object sender, RoutedEventArgs e)
        {
           OpenFileDialog openFileDialog = new OpenFileDialog();
           openFileDialog.Multiselect = true;
           // イニシャルデレクトリは適宜設定可
           openFileDialog.InitialDirectory = @"c:\";
           if (openFileDialog.ShowDialog() == true)
           {
               string[] filePaths = openFileDialog.FileNames;
               // 選択されたファイルからファイル名とパスを取得し、
               // LvFileNamesとLvFilePathsに格納する
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

        // 「クリア」ボタン
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
           // LvFileNamesとLvFilePathsについてそれぞれ要素を全て削除する
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

        // 「確定する」ボタン
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
           if (LvFileNames.Count > 0)
           {
               // 「追加する」ボタン・「クリア」ボタン・「確定する」ボタン　を不活性化する
               OpenfileButton.IsEnabled = false;
               ClearButton.IsEnabled = false;
               ConfirmButton.IsEnabled = false;
               // tempフォルダ内に、選択済ファイル格納用のユニーク名付きフォルダを作成する
               string guid = Guid.NewGuid().ToString();
               string dirPath = Path.GetFullPath(Environment.CurrentDirectory)
                   + @"\..\..\..\temp\" + guid + @"\";
               Directory.CreateDirectory(dirPath);
               // tempフォルダ内に選択済ファイルのコピーを作成する
               foreach (string path in LvFilePaths)
               {
                   File.Copy(path, dirPath + Path.GetFileName(path));
               }
               // Logクラスのインスタンスを作成する
               Log log = new Log(
                   DateTime.Now.ToString(),
                   guid,
                   Environment.UserName
                   );
               // データベースにレコードを追加する
               InsertData(log);

               MessageBox.Show("確定しました。終了してください");
           }
           else
           {
               MessageBox.Show("ファイルが選択されていません");
           }
        }

        // 「終了」ボタン
        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
           Close();
        }

        // データベースにレコードを追加する関数
        private void InsertData(Log log)
        {
           var connectionStringBuilder = new SQLiteConnectionStringBuilder();
           // データベースのパスを指定する
           connectionStringBuilder.DataSource = @"..\..\..\sqlite_db\myDb.db";
           // SQLを実行するコマンドとして、INSERT文を作成する
           string insertCmdText = "INSERT INTO request(date, guid, user, is_written) "
               + "VALUES('" + log.Date + "','" + log.Guid + "','" + log.User + "','notyet')";
           // データベースに接続してコマンドを実行する
           using (var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString))
           {
               connection.Open();
           // トランザクションとして実行する
               using (SQLiteTransaction transaction = connection.BeginTransaction())
               {
                   SQLiteCommand insertCmd = connection.CreateCommand();
                   insertCmd.CommandText = insertCmdText;
                   insertCmd.ExecuteNonQuery();
                   transaction.Commit();
               }
           }
        }
    }
}

