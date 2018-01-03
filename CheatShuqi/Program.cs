using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

namespace Shuqi
{
    class BookInfo
    {
        public string bookId;
        public string bookName;
        public string authorName;
        public string chapterNum;
        public List<ChapterInfo> chapterList;
    }

    class ChapterInfo
    {
        public string chapterId;
        public string chapterName;
        public string chapterOrdid;
    }

    class Program
    {
        /// <summary>
        /// 章节列表(手机浏览器)
        /// POST http://walden1.shuqireader.com/webapi/book/chapterlist
        /// timestamp=时间戳
        /// user_id = 用户名（似乎随便填）
        /// bookId=书id
        /// sign = md5(bookId + timestamp + user_id + "37e81a9d8f02596e1b895d07c171d5c9")
        /// 
        /// 章节内容(uc小说中抓包得到的)
        /// GET http://c1.shuqireader.com/httpserver/filecache/get_book_content_书id_章节id.xml
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            try
            {
                Console.Write("bookid:");
                string bookid = Console.ReadLine();
                var book = ReadBookInfo(bookid);

                Console.WriteLine("书名 : " + book.bookName);
                Console.WriteLine("作者 : " + book.authorName);
                Console.WriteLine("章节数 : " + book.chapterNum);
                Console.WriteLine("开始下载...");
                Thread.Sleep(2000);

                if (Directory.Exists(book.bookName) == false)
                    Directory.CreateDirectory(book.bookName);

                foreach (var item in book.chapterList)
                {
                    string content = ReadChapterContent(bookid, item);
                    string filename = item.chapterOrdid.PadLeft(5, '0') + "_" + item.chapterName + ".txt";
                    File.WriteAllText(Path.Combine(book.bookName, filename), content);
                    Console.WriteLine("下载完成 : " + filename);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            Thread.Sleep(2000);
        }

        public static BookInfo ReadBookInfo(string bookid)
        {
            while (true)
            {
                try
                {
                    string userid = "8000000";
                    long timestamp = 1514984538213;
                    string signcontent = string.Concat(bookid, timestamp, userid, "37e81a9d8f02596e1b895d07c171d5c9");
                    MD5 md5 = new MD5CryptoServiceProvider();
                    byte[] output = md5.ComputeHash(Encoding.UTF8.GetBytes(signcontent));
                    string byte2String = null;

                    for (int i = 0; i < output.Length; i++)
                    {
                        byte2String += output[i].ToString("x2");
                    }

                    byte[] postData = Encoding.UTF8.GetBytes(string.Format("timestamp={0}&user_id={1}&bookId={2}&sign={3}", timestamp, userid, bookid, byte2String));
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://walden1.shuqireader.com/webapi/book/chapterlist");
                    request.Method = "POST";
                    request.ContentType = "application/x-www-form-urlencoded";
                    request.ContentLength = postData.Length;
                    using (Stream requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(postData, 0, postData.Length);
                    }
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    StreamReader stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                    string srcString = stream.ReadToEnd();
                    response.Close();
                    stream.Close();

                    var content = fastJSON.JSON.Parse(srcString) as Dictionary<string, object>;
                    if (string.Equals(content["state"], "200") == false)
                    {
                        Console.WriteLine(content["message"]);
                        return null;
                    }

                    var data = content["data"] as Dictionary<string, object>;
                    var datacl = data["chapterList"] as List<object>;

                    BookInfo book = new BookInfo();
                    book.bookName = data["bookName"] as string;
                    book.authorName = data["authorName"] as string;
                    book.chapterNum = data["chapterNum"] as string;
                    book.chapterList = new List<ChapterInfo>();
                    foreach (var item in datacl)
                    {
                        var aaa = item as Dictionary<string, object>;
                        var volumeList = aaa["volumeList"] as List<object>;
                        foreach (var volumeRaw in volumeList)
                        {
                            var volume = volumeRaw as Dictionary<string, object>;
                            ChapterInfo info = new ChapterInfo();
                            info.chapterId = volume["chapterId"] as string;
                            info.chapterName = volume["chapterName"] as string;
                            info.chapterOrdid = volume["chapterOrdid"] as string;
                            book.chapterList.Add(info);
                        }
                    }

                    return book;
                }
                catch (Exception)
                {
                    Console.WriteLine("读取信息失败");
                }
            }
        }

        public static string ReadChapterContent(string bookid, ChapterInfo chapter)
        {
            while (true)
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("http://c1.shuqireader.com/httpserver/filecache/get_book_content_{0}_{1}.xml", bookid, chapter.chapterId));
                    request.Method = "GET";
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    StreamReader stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                    string srcString = stream.ReadToEnd();
                    response.Close();
                    stream.Close();

                    XmlDocument xmldoc = new XmlDocument();
                    xmldoc.LoadXml(srcString);

                    string badbase64 = xmldoc.LastChild.InnerText;

                    string content = DecodeChapterContent(badbase64);
                    return content.Replace("<br/>", "\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("下载失败, 章节 : {0}", chapter.chapterId);
                }
            }
        }

        public static string DecodeChapterContent(string code)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(code);
            for (int i = 0; i < bytes.Length; i++)
            {
                byte charAt = bytes[i];
                if ('A' <= charAt && charAt <= 'Z')
                {
                    charAt = (byte)(charAt + 13);
                    if (charAt > 'Z')
                    {
                        charAt = (byte)(((charAt % 90) + 65) - 1);
                    }
                }
                else if ('a' <= charAt && charAt <= 'z')
                {
                    charAt = (byte)(charAt + 13);
                    if (charAt > 'z')
                    {
                        charAt = (byte)(((charAt % 122) + 97) - 1);
                    }
                }
                bytes[i] = charAt;
            }
            code = System.Text.Encoding.UTF8.GetString(bytes);
            byte[] bbb = Convert.FromBase64String(code);
            return System.Text.Encoding.UTF8.GetString(bbb);
        }
    }
}
