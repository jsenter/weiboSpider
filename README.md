weiboSpider
===========
微博爬虫，C#代码
网上也有很多了，这个小爬虫的主要作用是拉指定用户的评论，包含本人微博上以及对话过得用户的微博上发出来的评论。
同时还有一些简单功能，拉指定用户的互粉好友。

C#工程，编译后运行需要一个微博账户文件来运行程序。可以多线程跑。一个线程需要2个账户（因为一个账户爬一会了新浪好像会检测到并禁用一段时间，所以需要一个备用账户互相切换）

账户文件是文本文件，格式如下：

微博账户,密码
微博账户,密码
...

程序有些地方检查的不仔细，有些数据有重复。其实有点用的地方就是一些匹配文本的正则表达式。
