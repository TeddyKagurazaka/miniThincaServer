# miniThincaServer
让你的SEGAY机器能刷上PASELI

# 使用方式
- ~~修改miniThincaServer的IP并启动~~ <br />(现在他会自动找电脑192开头的内网IP，或者带参启动，第一个参数)

- 找一张Mifare卡，写入thincaMod4.mct (MiFare Classic Tool)

- 修改env.json的root_endpoint，将地址指向 http://你的IP地址/thinca

- 修改resource.xml的 /thincaResource/common/commonPrimaryUri，将地址指向 http://你的IP地址/thinca/common-shop/

- (可选) 如果你缺少vfd，使用USB转COM连接机台的COM2后，打开vfdEmu

- 启动游戏

# 不能用怎么办
- 游戏-电子支付信息-机台认证-开始认证<br />
如果Aime读卡器没有闪烁蓝灯，检查VFD连接（转接线要求支持硬件流控(DTS/RTS)）<br />
<i>如果实在懒得动/没东西，可以爆改amDaemon.exe跳过vfd检测，字符串搜索ampdGd1232a01aInit()</i>

- 闪蓝灯之后无法读卡<br />
需要读专门的授权卡，用一个门卡写入thincaMod4.mct

- 机器叫了几声然后状态显示Error<br />
服务器没通，检查配置文件<br />

# 想整点花活怎么办
[这里尽可能给你解释花活](Explain.md)
