using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Globalization;
using System.Threading;

namespace ConsoleApplication2
{
    class Program
    {
        // 测试weibo.com
        static void weiboComTest()
        {
            string weiboComUrl = "";
            var cc1 = Weibo.GetWeiboComCC("XXXX@XXXX.com", "XXXXXX");
            var content = SpiderHelper.RequestHttpGetWithCookie(weiboComUrl, cc1);
        }

        class WeiboComment
        {
            public string userid;
            public string username;
            public string comment;
            public void dump()
            {

                string s = "{\"uid\":\"" + userid + "\", \"username\":\"" + username + "\", \"comment\":\"" + comment + "\"}";
                Console.WriteLine(s);

            }
            public string toString()
            {
                string s = "{\"uid\":\"" + userid + "\", \"username\":\"" + username + "\", \"comment\":\"" + comment + "\"}";
                return s;
            }
        }

        class WeiboUser
        {
            public string userid;
            public string username;
            public string url;
            public int count;

            public void dump()
            {
                Console.WriteLine("userid:" + userid + ", username:" + username + ", url:" + url);
            }
            public string toString()
            {
                return "{\"uid\":\"" + userid + "\", \"username\":\"" + username + "\", \"url\":\"" + url + "\"}";
            }
        }

        // 获取指定URL对象的所有微博入口
        static List<string> getTotalCommentEntities(CookieContainer cc, string url, int startPage, int endPage)
        {
            List<string> comments = new List<string>();
            int maxPage = 0;

            for (int i = startPage; i <= (maxPage == 0 ? endPage : maxPage); i++)
            //for (int i = 1; i <= 2; i++)
            {
                // 评论入口
                // 示例： <a href="http://weibo.cn/comment/AdJ58CAPm?uid=1981250484&amp;rl=0&amp;st=9771#cmtfrm" class="cc">评论[0]</a>
                string regstr = "<a href=\"([^\"]+)\" class=\"cc\">评论[[]+([0-9]+)";
                string content = "";
                try
                {
                    Console.Write("搜索第 " + i + " 页，抓取到 ");
                    content = SpiderHelper.RequestHttpGetWithCookie(url + i, cc);
                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
                    continue;
                }
                MatchCollection mc = Regex.Matches(content, regstr, RegexOptions.Compiled | RegexOptions.IgnoreCase);

                // 寻找最大页码，模式： "/134页</div>"
                if (maxPage == 0)
                {
                    Match maxpageMatch = Regex.Match(content, "/([0-9]+)页</div>");
                    if (maxpageMatch.Length > 0)
                    {
                        maxPage = int.Parse(maxpageMatch.Groups[1].Value);
                    }
                    maxPage = maxPage > endPage ? endPage : maxPage; // 超过200页的微博。应该是碰上大V了
                }

                int n = 0;
                foreach (Match m in mc)
                {
                    int commentsCount = int.Parse(m.Groups[2].Value);
                    if (commentsCount > 0)
                    {
                        comments.Add(m.Groups[1].Value);
                        n = n + 1;
                    }
                }
                Console.WriteLine(n + " 条评论入口.");
            }

            return comments;
        }

        class WeiboData
        {
            public WeiboData()
            {
                comments = new List<WeiboComment>();
                friends = new Dictionary<string, WeiboUser>();
            }
            public List<WeiboComment> comments;
            public Dictionary<string, WeiboUser> friends;
        }

        // 测试weibo.cn，抓取所有评论，包含目标微博上的所有评论，以及目标在所有评论者上的评论
        static void weiboCnGrabComments(List<CookieContainer> ccList, string uid, int startPage, int endPage)
        {
            // http://weibo.cn/u/uid?page=N
            string ulogin = "http://weibo.cn/u/" + uid + "?page=";


            // 获取博主自身的所有评论，以及所有在评论中出现过的好友数据
            WorkClass maincs = new WorkClass();
            maincs.cc0 = ccList[0];
            maincs.cc1 = ccList[1];
            maincs.startPage = startPage;
            maincs.endPage = endPage;
            maincs.threadName = "启动线程";
            maincs.selfuid = uid;
            WeiboData weiboData = maincs.getTotalComments(ulogin, uid, null);
            Console.WriteLine("1. 搜索到博主评论条数：" + weiboData.comments.Count + ", 评论过的好友个数：" + weiboData.friends.Count);

            // 计算每个线程能分到的好友数量
            int friendsCount = weiboData.friends.Count;
            int threadCount = ccList.Count / 2;
            int n = friendsCount / threadCount;
            bool haveExt = (friendsCount % (ccList.Count / 2)) > 0; // 检查最后一个线程是否有额外的好友数据需要处理
            List<WeiboUser> friends = weiboData.friends.Values.ToList();
            List<Thread> threads = new List<Thread>();
            List<WorkClass> works = new List<WorkClass>();

            // 启动线程，开始抓取好友中的评论
            for (int i = 0; i < threadCount; i++)
            {
                WorkClass wc = new WorkClass();
                wc.cc0 = ccList[2 * i];
                wc.cc1 = ccList[2 * i + 1];
                wc.threadName = "线程" + i;
                wc.startPage = startPage;
                wc.endPage = endPage;
                wc.selfuid = uid;
                int start = n * i;
                int end = (n == (threadCount - 1)) ? friendsCount : n * (i + 1);
                for (int m = start; m < end; m++)
                {
                    wc.friends.Add(friends[m]);
                }
                Thread t = new Thread(new ParameterizedThreadStart(wc.Run));
                t.Name = "线程" + i;
                t.Start();
                works.Add(wc);
                threads.Add(t);
            }

            // 等待线程结束
            while (true)
            {
                bool end = true;
                for (var i = 0; i < threads.Count; i++)
                {
                    if (threads[i].ThreadState == ThreadState.Running)
                    {
                        end = false;
                        continue;
                    }
                    if (!works[i].end)
                    {
                        weiboData.comments.AddRange(works[i].comments);
                        works[i].end = true;
                    }
                }
                if (end)
                {
                    break;
                }
            }

            Console.WriteLine("搜索完成,共 " + weiboData.comments.Count + " 评论.");

            
            var s = "";
            foreach (var m in weiboData.comments)
            {
                s += m.toString() + "\n";
            }
            File.WriteAllText(uid + "-comment-" + startPage + "-" + endPage + ".txt", s);
        }

        class WorkClass
        {
            public CookieContainer cc0 = null;
            public CookieContainer cc1 = null;
            public CookieContainer cc = null;
            public string threadName;
            public bool end = false;
            public List<WeiboUser> friends = new List<WeiboUser>();
            public List<WeiboComment> comments = new List<WeiboComment>();
            public string selfuid;
            public int startPage, endPage;

            public void Run(object data)
            {
                Console.WriteLine("线程" + threadName + "开始执行，本线程共要抓取 " + friends.Count + " 位好友数据");
                for (int i = 0; i < friends.Count; i++)
                {
                    var friend = friends[i];
                    Console.Write("[" + threadName + "]开始抓起好友[" + friend.username + "(" + friend.userid + ")]的评论:\n");
                    WeiboData friendWeiboData = getTotalComments(friend.url, selfuid, friend.userid);
                    Console.WriteLine("[" + threadName + "]从好友[" + friend.username + "(" + friend.userid + ")]共抓取评论 " + friendWeiboData.comments.Count + "条");
                    comments.AddRange(friendWeiboData.comments); // 将从好友微博中搜索到的评论加到自己评论集合中
                }
            }

            // 获取指定目标中的所有微博评论，如果指定了目标ID，则只获取指定目标ID的评论
            public WeiboData getTotalComments( string url, string selfId, string friendId)
            {
                cc = cc0;
                WeiboData weiboData = new WeiboData();

                List<string> commentEntities = getTotalCommentEntities(cc, url, startPage, endPage); // 得到微博评论入口

                // 遍历入口得到所有评论
                foreach (var entity in commentEntities)
                {
                    Console.Write("[" + threadName + "]评论入口:" + entity + " ");
                    int n = 0;
                    int maxCommentPage = 0;
                    for (int i = 1; i <= (maxCommentPage == 0 ? 2 : maxCommentPage); i++)
                    {
                        string content = "";
                        try
                        {
                            content = SpiderHelper.RequestHttpGetWithCookie(entity + "&page=" + i, cc);
                        }
                        catch (Exception e)
                        {
                            Console.Write(e.Message);
                            continue;
                        }

                        // 查找评论的最大页码
                        if (maxCommentPage == 0)
                        {
                            Match regMaxCommentPage = Regex.Match(content, "/([0-9]+)页</div>");
                            if (regMaxCommentPage.Length > 0)
                            {
                                maxCommentPage = int.Parse(regMaxCommentPage.Groups[1].Value);
                            }
                            maxCommentPage = maxCommentPage > 10 ? 10 : maxCommentPage; // 评论超过 10 页，应该也是大V没错，惹不起
                        }

                        // 
                        // 评论示例：<a href="/u/1990224584?st=9771">目标用户</a>:<span class="ctt">哈哈，眼熟吧</span>
                        // .*? 这种写法是因为c#的正则表达式匹配默认是贪婪模式，默认匹配最多字符，用这个可以禁止贪婪模式

                        string regstr = "<a href=\"/u/([0-9]+).*?\">(.*?)</a>.*?<span class=\"ctt\">.*?</span>";
                        MatchCollection mc = Regex.Matches(content, regstr, RegexOptions.Compiled | RegexOptions.IgnoreCase);

                        if (mc.Count == 0)
                        {
                            cc = (cc == cc0) ? cc1 : cc0; // COOKIE已经被检测到异常，换一个COOKIE继续
                        }
                        foreach (Match m in mc)
                        {
                            WeiboComment comment = new WeiboComment();
                            comment.userid = m.Groups[1].Value;
                            string s = m.Value.Replace("</span>", "");
                            int k = s.LastIndexOf('>');
                            comment.comment = s.Substring(k + 1, s.Length - k - 1);
                            comment.username = m.Groups[2].Value;

                            n++;

                            if (friendId == null)
                            {   // 此时是获取博主自身的
                                weiboData.comments.Add(comment);
                                if (!comment.userid.Equals(selfId))
                                {  // 有新的好友
                                    WeiboUser friend;
                                    weiboData.friends.TryGetValue(comment.userid, out friend);
                                    if (friend == null)
                                    {
                                        friend = new WeiboUser();
                                        friend.userid = comment.userid;
                                        friend.url = "http://weibo.cn/u/" + friend.userid + "?page=";
                                        friend.username = comment.username;
                                        weiboData.friends.Add(friend.userid, friend);
                                    }
                                    friend.count++;
                                }
                                continue;
                            }
                            else
                            {   // 此时是获取博主好友的，就只保存博主和好友说过的
                                if (comment.userid.Equals(selfId)) // || comment.userid.Equals(friendId))
                                {
                                    weiboData.comments.Add(comment);
                                }
                            }
                        }
                    }
                    Console.WriteLine(n + " 条评论");
                }

                return weiboData;
            }
        }

        class WeiboFan
        {
            public string uid;
            public string username;
            public string entryUrl;
            public string toString()
            {
                return "{\"uid\":\"" + uid + "\", \"username\":\"" + username + "\", \"url\":\"" + entryUrl + "\"}";
            }
        }
        // 抓取目标的所有微博粉丝，好友
        static List<WeiboFan> weiboCnGrabFansAndfollow(CookieContainer cc0, CookieContainer cc1, string uid, string type)
        {
            CookieContainer cc = cc0;
            string url = "http://weibo.cn/" + uid + "/" + type;
            int maxpage = 0;
            List<WeiboFan> fans = new List<WeiboFan>();

            for (int i = 1; i < (maxpage == 0 ? 2 : maxpage); i++)
            {
                string content = "";
                try
                {
                    content = SpiderHelper.RequestHttpGetWithCookie(url + "?page=" + i, cc);
                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
                    continue;
                }

                // 查找评论的最大页码
                if (maxpage == 0)
                {
                    Match regexMaxPage = Regex.Match(content, "/([0-9]+)页</div>");
                    if (regexMaxPage.Length > 0)
                    {
                        maxpage = int.Parse(regexMaxPage.Groups[1].Value);
                    }
                }

                string regstr = "<td valign=\"top\" style=\"width: 52px\">.*?</td><td valign=\"top\"><a href=\"(.*?)\">(.*?)</a>.*?</td>";
                MatchCollection mc = Regex.Matches(content, regstr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (mc.Count == 0) 
                {
                    cc = (cc == cc0) ? cc1 : cc0;
                    continue;
                }
                foreach (Match m in mc)
                {
                    WeiboFan fan = new WeiboFan();
                    fan.username = m.Groups[2].Value;
                    fan.entryUrl = m.Groups[1].Value;
                    if (fan.entryUrl.Contains("/u/"))
                    {
                        Match matchUid = Regex.Match(fan.entryUrl, "http://weibo.cn/u/([0-9]+).*");
                        if (matchUid.Length > 0)
                        {
                            fan.uid = matchUid.Groups[1].Value;
                        }
                    }
                    else
                    {
                        fan.uid = "6969696969";
                    }
                    fans.Add(fan);
                }
            }

            string s = "";
            for (var i = 0; i < fans.Count-1; i++)
            {
                s += fans[i].toString() + ",\r\n";
            }
            s += fans[fans.Count - 1].toString();
            File.WriteAllText(uid + "-" + type + ".txt", s);

            return fans;
        }

        // 抓取目标的互粉好友
        static List<WeiboFan> weiboCnGrabfollowFans(CookieContainer cc0, CookieContainer cc1, string uid)
        {
            List<WeiboFan> rst = new List<WeiboFan>();
            List<WeiboFan> fans = weiboCnGrabFansAndfollow(cc0, cc1, uid, "fans");
            List<WeiboFan> follows = weiboCnGrabFansAndfollow(cc0, cc1, uid, "follow");
            foreach (var f in fans)
            {
                foreach (var fe in follows)
                {
                    if (f.username.Equals(fe.username))
                    {
                        rst.Add(f);
                    }
                }
            }

            string s = "";
            for (var i = 0; i < rst.Count - 1; i++)
            {
                s += rst[i].toString() + ",\r\n";
            }
            s += rst[rst.Count - 1].toString();
            File.WriteAllText(uid + "-followfans.txt", s);

            return rst;
        }

        static void printHelp()
        {
            Console.WriteLine("获取评论：程序名 comment 账户文件 目标UID 开始页码 结束页码 ");
            Console.WriteLine("获取粉丝：程序名 fans 账户文件 目标UID");
            Console.WriteLine("获取关注：程序名 follow 账户文件 目标UID");
            Console.WriteLine("获取互粉：程序名 followfans 账户文件 目标UID");
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                printHelp();
                return;
            }
            string type = args[0];
            if (type.Equals("comment") && args.Length != 5)
            {
                Console.WriteLine("获取评论：程序名 comment 账户文件 目标UID 开始页码 结束页码 ");
                return;
            }
            else if (type.Equals("fans") && args.Length != 3)
            {
                Console.WriteLine("获取粉丝：程序名 fans 账户文件 目标UID");
                return;
            }
            else if (type.Equals("follow") && args.Length != 3)
            {
                Console.WriteLine("获取关注：程序名 follow 账户文件 目标UID");
                return;
            }
            else if (type.Equals("followfans") && args.Length != 3)
            {
                Console.WriteLine("获取互粉：程序名 followfans 账户文件 目标UID");
                return;
            }

            if (!File.Exists(args[1]))
            {
                Console.WriteLine("指定的账户文件" + args[1] + "不存在.");
                return;
            }

            // 初始化所有COOKIE
            List<CookieContainer> ccList = new List<CookieContainer>();
            try
            {
                string[] accounts = File.ReadAllLines(args[1]);
                for (int i = 0; i < accounts.Length; i++)
                {
                    string[] accountStr = accounts[i].Split(',');
                    ccList.Add(WeiboCn.getWeiboCnCC(accountStr[0], accountStr[1]));
                }
                if (accounts.Length < 2)
                {
                    Console.WriteLine("至少要有2个微博账户才能开扫.");
                    return;
                }
                Console.WriteLine("最大线程数：" + (accounts.Length / 2));
            }
            catch (Exception e)
            {
                Console.WriteLine("初始化账户文件失败，异常信息：" + e.Message);
                return;
            }

            string uid = args[2];
            Console.WriteLine("操作类型：" + type + ", 目标UID：" + uid);

            if (type.Equals("comment"))
            {
                int startPage = int.Parse(args[3]);
                int endPage = int.Parse(args[4]);
                weiboCnGrabComments(ccList, uid, startPage, endPage);
            }
            else if (type.Equals("fans") || type.Equals("follow"))
            {
                weiboCnGrabFansAndfollow(ccList[0], ccList[1], uid, type);
            }
            else if (type.Equals("followfans"))
            {
                weiboCnGrabfollowFans(ccList[0], ccList[1], uid);
            }
            //weiboComTest();
            

            Console.WriteLine("按回车键退出...");
            Console.ReadKey();
        }
    }
}
