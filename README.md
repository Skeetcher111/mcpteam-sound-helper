# MCPteam Звуковой помощник — ровная громкость в Windows

Раньше программа называлась «Ровно». Старые ссылки на github.com/Skeetcher111/rovno ведут сюда.

**[Скачать бесплатно (MCPteamSoundHelper.exe, 38 КБ)](https://github.com/Skeetcher111/mcpteam-sound-helper/releases/latest/download/MCPteamSoundHelper.exe)** · [English below](#english)

Один ролик на YouTube еле слышно, следующий оглушает. В фильме шепчут, потом взрыв, и рука снова тянется к регулятору. Звуковой помощник делает это за вас: тихое поднимает, громкое убавляет, резкий скачок срезает сразу. Работает со всеми программами: браузер, плеер, игры, Телеграм.

![До и после на фильме Tears of Steel](docs/demo.gif)

## Как пользоваться

1. Скачайте `MCPteamSoundHelper.exe` и запустите. Установка не нужна.
2. В трее появится оранжевый значок. Всё, громкость уже выравнивается.
3. Громкость Windows и колонок ставьте как вам удобно. Помощник их не трогает: ползунок Windows стоит там, где вы его оставили, а программа выравнивает тихое и громкое внутри звука каждой программы.

Клик по значку открывает настройки:

![Окно настроек](docs/settings.png)

- **Держать громкость на уровне** — один ползунок. Поставьте его один раз, и любое видео, фильм или программа будут звучать на этом уровне: громкий ролик убавится, тихий пойдёт на полной громкости программы, при смене видео уровень ловится за 1–2 секунды.
- В меню значка: вкл/выкл, автозапуск с Windows (включается при первом запуске), выход. При выходе громкость программ в микшере возвращается как была.

Как настроить один раз. Включите обычный ролик и подберите громкость колонками или Windows, как вам удобно. Если громкие ролики стали тише, чем хотелось, прибавьте колонки: дальше всё будет звучать на этой громкости. Если тихие ролики всё равно тише остальных, сдвиньте ползунок левее и ещё чуть прибавьте колонки. Громче 100% своей громкости программа звук поднять не может, поэтому уровень ползунка должен быть не выше самого тихого, что вы смотрите.

Что делала программа, видно в `%APPDATA%\MCPteam Sound Helper\log.txt`: строка раз в 2 секунды, пока играет звук (громкость звука, поправка, что вышло).

## Чем отличается

- **Без драйверов и системных модулей.** Ничего не встраивается в звуковую карту, не нужна перезагрузка, нечего удалять. Один файл на 38 КБ.
- **Нулевая задержка.** Звук не проходит через программу, поэтому губы в видео не отстают от голоса.
- **Сам переключается** на наушники, HDMI или Bluetooth, когда меняется устройство вывода.
- **Ваша громкость остаётся вашей.** Общий ползунок Windows программа не двигает никогда.
- **Один ползунок** вместо конфигов и плагинов.
- **И поднимает, и убавляет.** Обычный ограничитель только срезает громкое, а тихий ролик так и остаётся тихим.

## Как это устроено

Звуковой помощник не пропускает звук через себя. Программа слушает общий звук системы (WASAPI loopback), меряет громкость по стандарту ITU-R BS.1770 (тот же LUFS, по которому нормализуют YouTube и ТВ) и плавно двигает громкость каждой играющей программы в микшере Windows (тот же ползунок, что в «Микшере громкости»). Общий регулятор Windows только читается и не меняется:

- внутри одного видео поднимает не быстрее 6 дБ в секунду, без «качания»;
- если звук за 0,4 с ушёл выше уровня на 2 дБ (началось громкое видео), убавляет до 20 дБ в секунду;
- если звук за 50 мс прыгнул выше уровня больше чем на 4 дБ (взрыв, реклама, уведомление), режет сразу;
- после паузы от 0,6 с (переключили видео) 2 секунды подстраивается быстро, поэтому новое видео не играет с поправкой от прошлого;
- новая вкладка или плеер получает текущую поправку за 0,3 с;
- в тишине и паузах ничего не трогает, поэтому шум не вытягивается.

Раз звук идёт мимо программы, у видео нет никакой задержки к картинке, а вылет программы не оставит вас без звука. Цена такого подхода: первые 20–30 мс резкого удара успевают пройти до того, как громкость сдвинется, а в «Микшере громкости» ползунки программ ходят сами. Свою громкость программы в микшере можно менять как обычно, помощник запомнит её. Если программу закрыли посреди громкой сцены, Windows запомнит убавленную громкость, а помощник при следующем запуске вернёт вашу.

## Замеры

Модель проверена на реальном звуке тем же кодом (`MCPteamSoundHelper.exe --sim вход.wav выход.wav`), что работает в трее, с задержкой регулятора 20 мс. Громкость мерил ffmpeg (ebur128), скрипт `tests/measure.py`.

| Пример | Было | Стало |
|---|---|---|
| Фильм Tears of Steel, 12 мин (версия 1.1): разброс кратковременной громкости (5–95%) | 23 дБ (−30…−7 LUFS) | 10 дБ (−26…−17 LUFS) |
| Тот же фильм (версия 1.1), диапазон громкости LRA | 20,8 LU | 8,0 LU |
| Трейлер, громкий ролик, снова трейлер по 100 с (версия 1.3) | −24,0 / −7,1 / −24,0 LUFS, разница 17 дБ | −24,8 / −24,1 / −24,7 LUFS, разница 0,7 дБ |
| То же в версии 1.2.4 (только убавляла) | | −24,0 / −17,8 / −24,3 LUFS, разница 6 дБ |

Живой тест версии 1.3 на Windows 10, громкость Windows 100%: тихий отрывок, громкий, снова тихий по 15 с с паузой 1 с. На выходе −24,7 / −23,6 / −24,0 LUFS, после переключения уровень ловится за 1–2 с. Громкость Windows все 50 секунд стояла на 1,00.

## Сборка из исходников

Нужен только компилятор C#, который уже есть в Windows 10 и 11:

```
build.cmd
```

Весь код в одном файле `src/SoundHelper.cs`, без сторонних библиотек.

Служебные режимы: `MCPteamSoundHelper.exe --sim in.wav out.wav [запас_дБ]` прогоняет файл через тот же алгоритм, `MCPteamSoundHelper.exe --probe` печатает измеренную громкость и громкость Windows (она не должна меняться).

## Если что-то не так

- Windows может показать «Система Windows защитила ваш компьютер»: файл новый и без платной подписи. Нажмите «Подробнее» → «Выполнить в любом случае». Исходники открыты, можно собрать самому.
- У некоторых звуковых карт loopback идёт до регулятора громкости. Если звук начинает «гулять», добавьте строку `loopback=pre` в `%APPDATA%\MCPteam Sound Helper\settings.ini` и перезапустите.
- Ошибки и идеи — в [Issues](https://github.com/Skeetcher111/mcpteam-sound-helper/issues).

Сделано в [MCPteam](https://mcpteam.ru) — сайты, приложения и программы на заказ. Лицензия MIT. Тестовый фильм — Tears of Steel, (CC) Blender Foundation, mango.blender.org.

---

<a name="english"></a>
# MCPteam Sound Helper — even volume for Windows

Formerly called Rovno.

**[Download for free (MCPteamSoundHelper.exe, 38 KB)](https://github.com/Skeetcher111/mcpteam-sound-helper/releases/latest/download/MCPteamSoundHelper.exe)**

One video is too quiet, the next one is deafening; a movie whispers and then explodes. MCPteam Sound Helper keeps all system sound at about the same loudness: it lifts quiet content, lowers loud content and cuts sudden bangs at once. Works with every app.

- **No driver, no APO, no reboot.** A 38 KB tray app.
- **Zero added latency.** Audio never passes through the app: it measures the system mix (WASAPI loopback, ITU-R BS.1770 / LUFS) and rides the per-app volumes in the Windows Volume Mixer. The Windows master volume is never touched.
- One slider, "keep the volume at this level": every video and app is brought to it. Up 6 dB/s inside a video, down up to 20 dB/s when a louder video starts, instant guard at +4 dB within 50 ms, fast 2 s re-learn after a pause of 0.6 s+ (video switch). Silence is ignored, so noise is not pumped up.
- Follows the default output device (headphones, HDMI, Bluetooth). Restores your volume on exit.
- Set Windows and speaker volume as you like, the app leaves them alone. An app can't go above 100 % of its own volume, so set the level no higher than the quietest content you watch and turn the speakers up once. A log of what it did is in `%APPDATA%\MCPteam Sound Helper\log.txt`.

Measured with the same controller code (`--sim`): on the 12‑minute film *Tears of Steel* (v1.1) the short‑term loudness spread went from 23 dB to 10 dB (LRA 20.8 → 8.0 LU); a trailer, a loud video and the trailer again (v1.3) went from a 17 dB gap to 0.7 dB (v1.2.4: 6 dB).

Trade-off: the first 20–30 ms of a sudden bang pass before the volume moves, and app sliders in the Volume Mixer move by themselves.

Build: run `build.cmd` (uses the C# compiler shipped with Windows). Single file, no dependencies. MIT license. Made by [MCPteam](https://mcpteam.ru).
