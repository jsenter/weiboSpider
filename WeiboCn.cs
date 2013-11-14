using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace ConsoleApplication2
{
    class WeiboCn
    {
        // 获取weibo.cn的COOKIE
        public static CookieContainer getWeiboCnCC(string username, string passwd)
        {
            // 获取登录表单
            weibocn_form form = getWeiboCnFormId();
            var su = HttpUtility.UrlEncode(username);
            var postData = "mobile=" + su + "&password_" + form.formid + "="+passwd+"&remember=on";
            postData += "&backURL=http%253A%252F%252Fweibo.cn%252F%253Fs2w%253Dlogin";
            postData += "&backTitle=%E6%96%B0%E6%B5%AA%E5%BE%AE%E5%8D%9A&tryCount=";
            postData += "&vk=" + form.vk + "&submit=%E7%99%BB%E5%BD%95";

            string url = "http://login.weibo.cn/login/" + form.action;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.CookieContainer = new CookieContainer();
            req.CookieContainer.Add(new Uri(url), new Cookie("lang", "zh-cn"));
            req.Method = "post";
            req.Referer = "http://login.weibo.cn/login/?ns=1&revalid=2&backURL=http%3A%2F%2Fweibo.cn%2F&backTitle=%D0%C2%C0%CB%CE%A2%B2%A9&vt=";
            req.ContentType = "application/x-www-form-urlencoded";
            req.KeepAlive = true;
            req.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/30.0.1599.69 Safari/537.36";
            //req.Headers.Add("Accept-Encoding", "gzip,deflate,sdch");
            req.Headers.Add("Accept-Language", "zh-CN,zh;q=0.8");
            req.Headers.Add("Origin", "http://login.weibo.cn");
            req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";

            // 写入POST
            byte[] sendBytes = Encoding.UTF8.GetBytes(postData.Trim());
            req.ContentLength = sendBytes.Length;

            //提交请求
            Stream stream = req.GetRequestStream();
            stream.Write(sendBytes, 0, sendBytes.Length);
            stream.Close();

            req.Timeout = 10 * 1000;
            HttpWebResponse response = (HttpWebResponse)req.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("ISO-8859-1"));
            string content = sr.ReadToEnd();
            response.Close();

            //利用cookie进入微博
            var cookieCollection = req.CookieContainer.GetCookies(new Uri(url));
            var cc = new CookieContainer();
            cc.Add(cookieCollection);
            return cc;
        }

        private class weibocn_form
        {
            public string action;
            public string formid;
            public string vk;
        }

        private static weibocn_form getWeiboCnFormId()
        {
            weibocn_form formdata = new weibocn_form();

            string url = "http://login.weibo.cn/login/?ns=1&revalid=2&backURL=http%3A%2F%2Fweibo.cn%2F&backTitle=%D0%C2%C0%CB%CE%A2%B2%A9&vt=4";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.CookieContainer = new CookieContainer();
            req.CookieContainer.Add(new Uri(url), new Cookie("lang", "zh-cn"));
            req.Method = "get";
            req.Referer = "http://weibo.cn/pub/";
            req.KeepAlive = true;
            req.UserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/30.0.1599.69 Safari/537.36";
            //req.Headers.Add("Accept-Encoding", "gzip,deflate,sdch");
            req.Headers.Add("Accept-Language", "zh-CN,zh;q=0.8");
            req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";

            req.Timeout = 10 * 1000;
            HttpWebResponse response = (HttpWebResponse)req.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("ISO-8859-1"));
            string content = sr.ReadToEnd();
            response.Close();

            Match formidMatch = Regex.Match(content, "password_[0-9]+");
            if (formidMatch.Length > 0)
            {
                string[] s = formidMatch.Value.Split('_');
                formdata.formid = s[1];
            }

            Match formaction = Regex.Match(content, "action=\"[^\"]+\"");
            if (formaction.Length > 0)
            {
                formdata.action = formaction.Value.Substring(8, formaction.Length - 9);
                formdata.action = formdata.action.Replace("&amp;", "&");
            }

            Match vk = Regex.Match(content, "vk\" value=\"[^\"]+\"");
            if (vk.Length > 0)
            {
                formdata.vk = vk.Value.Substring(11, vk.Value.Length - 12);
            }

            return formdata;
        }

    }
}
