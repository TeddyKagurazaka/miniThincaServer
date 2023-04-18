# miniThincaServer
mini ThincaPayment Server

# 使用方式
- 修改miniThincaServer的IP并启动

- 找一张Mifare卡，写入thincaMod4.mct (MiFare Classic Tool)

- 修改env.json的root_endpoint，将地址指向 http://你的IP地址/thinca

- 修改resource.xml的 /thincaResource/common/commonPrimaryUri，将地址指向 http://你的IP地址/thinca/common-shop/

- (可选) 如果你缺少vfd，使用USB转COM连接机台的COM2后，打开vfdEmu

- 启动游戏