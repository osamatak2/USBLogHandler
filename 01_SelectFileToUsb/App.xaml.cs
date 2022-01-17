using System;
using System.IO;
using System.Windows;
using System.Linq;

namespace SelectFileToUsb
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // 終了時の後始末（排他制御のためのファイルの削除）
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // 自分のユーザー名のテキストファイルが残っていれば削除する
            string myFileName = Environment.UserName + ".txt";
            string[] files = Directory.GetFiles(@"..\..\..\semaphore\", "*");

            foreach(string file in files){
                if(Path.GetFileName(file) == myFileName)
                {
                    string fullPath = Path.GetFullPath(file);
                    File.Delete(fullPath);
                }
            }
        }
    }
}
