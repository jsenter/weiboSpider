using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace ConsoleApplication2
{
    class SpiderHelper
    {
        // 通过GET获取页面数据
        public static string RequestHttpGetWithCookie(string url, CookieContainer cc)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.CookieContainer = new CookieContainer();
            var cookies = GetAllCookies(cc);
            foreach (var cookie in cookies)
            {
                req.CookieContainer.Add(new Uri(url), new Cookie(cookie.Name, cookie.Value));
            }

            req.Method = "get";
            //req.Referer = "http://weibo.com/box?leftnav=1&wvr=5";
            req.KeepAlive = true;
            req.UserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/30.0.1599.69 Safari/537.36";
            //req.Headers.Add("Accept-Encoding", "gzip,deflate,sdch");
            req.Headers.Add("Accept-Language", "zh-CN,zh;q=0.8");

            //接收响应
            req.Timeout = 10 * 1000;
            HttpWebResponse response = (HttpWebResponse)req.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
            string content = sr.ReadToEnd();
            content = decodeUnicode(content);
            response.Close();

            return content;
        }

        public static List<Cookie> GetAllCookies(CookieContainer cc)
        {
            List<Cookie> lstCookies = new List<Cookie>();

            Hashtable table = (Hashtable)cc.GetType().InvokeMember("m_domainTable",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetField |
                System.Reflection.BindingFlags.Instance, null, cc, new object[] { });

            foreach (object pathList in table.Values)
            {
                SortedList lstCookieCol = (SortedList)pathList.GetType().InvokeMember("m_list",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetField
                    | System.Reflection.BindingFlags.Instance, null, pathList, new object[] { });
                foreach (CookieCollection colCookies in lstCookieCol.Values)
                    foreach (Cookie c in colCookies) lstCookies.Add(c);
            }

            return lstCookies;
        }

        // 对HTML数据的unicode编码进行转换
        public static string decodeUnicode(string content)
        {
            string s = HttpUtility.HtmlDecode(content);
            string s2 = HttpUtility.UrlDecode(s);
            string s3 = s2.Substring(0, s2.Length);

            MatchCollection mc = Regex.Matches(s2, @"\\u([\w]{2})([\w]{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            byte[] bts = new byte[2];
            foreach (Match m in mc)
            {
                bts[0] = (byte)int.Parse(m.Groups[2].Value, NumberStyles.HexNumber);
                bts[1] = (byte)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
                string r = Encoding.Unicode.GetString(bts);
                string a = "\\u" + m.Groups[1].Value + m.Groups[2].Value;
                s3 = s3.Replace(a, r);
            }

            s3 = s3.Replace(@"\\\", "");
            s3 = s3.Replace(@"\t", "");
            s3 = s3.Replace(@"\\/", "/");
            s3 = s3.Replace("  ", "");
            s3 = s3.Replace("\\n", "\n");
            s3 = s3.Replace("\\\"", "\"");
            s3 = s3.Replace("\\/", "/");

            return s3;
        }
    }
}
