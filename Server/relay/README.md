# DeepSleep 私人双人中继

这是转发服务，不需要 Unity 服务账号，不含付费接口。尚未部署到公网。

## 本机运行

在此目录执行：

```powershell
python -m venv .venv
.venv/Scripts/python -m pip install -r requirements.txt
.venv/Scripts/python -m unittest -v test_relay
.venv/Scripts/python relay.py
```

游戏选择 WSS relay，地址 `ws://127.0.0.1:8765`。房主创建后分享房间码，客人填写相同房间码。部署前先完成此本机验收。

## 单机服务器部署模板

## Render 免费公网测试

创建 Web Service，连接本仓库 main 分支：Language 选 Docker，Root Directory 为 `Server/relay`，Dockerfile Path 为 `Dockerfile`，Docker Context 为 `.`，实例选 Free，地区选 Singapore。无需额外填写启动命令；Dockerfile 已配置监听 `0.0.0.0`，程序读取平台提供的 `PORT`。健康检查路径设为 `/health`。

部署成功后先打开 `https://服务域名/health`，应返回 `ok`。游戏两端填写同一个 `wss://服务域名`，不加 `/health`。可运行 `python smoke_public.py wss://服务域名` 验证房间注册和双向转发；这不替代真实手机与电脑的游戏验收。

免费服务闲置后会休眠，首次唤醒可能约一分钟。先用浏览器等待健康检查返回，再在游戏里创建房间。服务重启会丢失房间。国内移动网络延迟和可达性待实测，不承诺正式运营质量。

## 自托管 Docker

在选择并购买服务器后，把本目录复制过去。Docker 示例：

```sh
docker build -t deepsleep-relay .
docker run -d --name deepsleep-relay --restart unless-stopped \
  --memory=256m --cpus=1 --pids-limit=128 \
  -p 127.0.0.1:8765:8765 deepsleep-relay
```

通过 Caddy/Nginx 在 443 上提供受信任证书的 WSS，参照 `Caddyfile.example`，替换自己已配置 DNS 的域名。防火墙不要把容器 8765 直接公开；游戏只填写 `wss://你的域名`。客户端只允许公网 WSS，明文 WS 仅允许 loopback。域名、证书、服务器帐号和公网连通性需拥有者提供后验收。

进程退出会清空所有房间，无房主迁移、数据库或持久对局。单实例容量默认32房间；`MAX_ROOMS` 和 `BYTES_PER_SECOND` 可通过环境变量设置。每条连接默认每秒最多1 MiB、每包16 KiB、同IP最多8连接、总连接128；心跳10秒。不要把这些限制当作已经压测得到的承载量。

房间码默认由客户端生成80-bit随机邀请串；首位客人绑定重连票据。只适用于私人邀请，不是公开匿名无限免费服务。对外推广前补充账户鉴权、用户配额、IP限流代理配置、费用报警及监控。反向代理后服务看到的可能全是代理IP，需要基于可信代理重新制定限流，不得盲目信任客户端 X-Forwarded-For。

服务器不记录包内容、房间码和重连票据。日志仅显示创建/加入/拒绝结果。WSS保护传输，不能阻止房主作弊（本方案明确为房主权威）。

API参考：[websockets serve](https://websockets.readthedocs.io/en/stable/reference/asyncio/server.html)、[ClientWebSocket并发限制](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket.receiveasync)。客户端发送由单队列串行化，同一时刻仅一个发送与一个接收。
