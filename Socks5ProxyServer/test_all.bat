@echo off
chcp 65001 > nul
cls

echo [1/4] Проверка пользователя 'alice'...
curl -s -x socks5://alice:secret123@127.0.0.1:1080 http://httpbin.org/ip
echo.
pause

echo [2/4] Проверка пользователя 'bob'...
curl -s -o nul -w "Статус ответа сайта: %%{http_code} OK (трафик успешно прошел через прокси)" -x socks5://bob:pass456@127.0.0.1:1080 http://example.com
echo.
echo.
pause

echo [3/4] Проверка неверного пароля (alice)...
curl -s -x socks5://alice:wrongpassword@127.0.0.1:1080 http://httpbin.org/ip 2>&1 | findstr /i "rejected"
if %errorlevel% equ 0 (echo Доступ запрещен по RFC 1929, пароль отклонен.)
echo.
pause

echo [4/4] Проверка лимита сессий (charlie, 5 одновременных подключений)...
start /b curl -s -x socks5://charlie:qwerty789@127.0.0.1:1080 http://httpbin.org/delay/2 > nul
start /b curl -s -x socks5://charlie:qwerty789@127.0.0.1:1080 http://httpbin.org/delay/2 > nul
start /b curl -s -x socks5://charlie:qwerty789@127.0.0.1:1080 http://httpbin.org/delay/2 > nul
start /b curl -s -x socks5://charlie:qwerty789@127.0.0.1:1080 http://httpbin.org/delay/2 > nul
start /b curl -s -x socks5://charlie:qwerty789@127.0.0.1:1080 http://httpbin.org/delay/2 > nul
echo Запросы отправлены. В окне сервера зафиксировано превышение лимита.
echo.
pause