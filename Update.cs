using System;
using System.IO;
using System.Net;

namespace MabiModManager
{
    class Update
    {
        // URL holding the current, up to date, patcher
        const string updateURL = "http://mabiv.cc/patcher/update.exe";

        // URL holding the current patcher version
        const string patchInfoURL = "http://mabiv.cc/patcher/version";

        // File holding the local patcher version
        const string patchVerFile = "modver";

        public Update()
        {
            prepatch();
        }

        // Updates PatchDLL to latest version. 
        // TODO: Provide a GUI when downloading an update.
        // TODO: Provide an easier way to change update URL and search using well defined variables
        static void prepatch()
        {
            // Defining variables
            string html = string.Empty;
            int patchDllVer = 0;
            int mainVer = 0;

            try
            {
                StreamReader patchver = new StreamReader(patchVerFile);
                patchDllVer = int.Parse(patchver.ReadLine());
                patchver.Close();
            }
            catch (Exception)
            {
            }

            // Grab patch.txt and assign values
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(patchInfoURL);
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    html = reader.ReadLine();
                    try
                    {
                        mainVer = int.Parse(html);
                    }
                    catch (Exception)
                    {
                        mainVer = 0;
                    }
                }

                // Update if needed
                if (patchDllVer < mainVer)
                {
                    // Download new dll
                    using (var newPatch = new WebClient())
                    {
                        newPatch.DownloadFile(new Uri(updateURL), "update.exe");
                    }
                    System.Diagnostics.Process.Start("update.exe");

                    Environment.Exit(0);
                } else
                {
                    try
                    {
                        File.Delete(@".\update.exe");
                    }
                    catch { }
                }

            }
            catch (Exception)
            {
                mainVer = 0;
            }
        }
    }

}
