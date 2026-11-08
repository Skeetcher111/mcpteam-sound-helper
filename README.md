# Ровно — ровная громкость в Windows

**[Скачать бесплатно (Rovno.exe, 37 КБ)](https://github.com/Skeetcher111/rovno/releases/latest/download/Rovno.exe)** · [English below](#english)

Один ролик на YouTube еле слышно, следующий оглушает. В фильме шепчут, потом взрыв, и рука снова тянется к регулятору. Ровно делает это за вас: тихое поднимает, громкое убавляет, резкий скачок срезает сразу. Работает со всеми программами: браузер, плеер, игры, Телеграм.

![До и после на фильме Tears of Steel](docs/demo.gif)

## Как пользоваться

1. Скачайте `Rovno.exe` и запустите. Установка не нужна.
2. В трее появится оранжевый значок. Всё, громкость уже выравнивается.
3. Громкость Windows и колонок ставьте как вам удобно. Ровно их не трогает: ползунок Windows стоит там, где вы его оставили, а программа выравнивает тихое и громкое внутри звука каждой программы.

Клик по значку открывает настройки:

![Окно настроек](docs/settings.png)

- **Целевая громкость** — на каком уровне держать звук.
- **Сила выравнивания** — 100% выравнивает почти всё, 50% оставляет фильму часть его динамики.
- В меню значка: вкл/выкл, автозапуск с Windows (включается при первом запуске), выход. При выходе громкость программ в микшере возвращается как была.

Чтобы было куда поднимать тихое, Ровно держит обычный звук на 6 дБ ниже, чем стоит программа в микшере, а тихому эти 6 дБ отдаёт. Поэтому после включения всё звучит чуть тише: один раз прибавьте колонки, и дальше уровень стоит. Запас меняется строкой `reserve=6` в `%APPDATA%\Rovno\settings.ini`.

## Чем отличается

- **Без драйверов и системных модулей.** Ничего не встраивается в звуковую карту, не нужна перезагрузка, нечего удалять. Один файл на 37 КБ.
- **Нулевая задержка.** Звук не проходит через программу, поэтому губы в видео не отстают от голоса.
- **Сам переключается** на наушники, HDMI или Bluetooth, когда меняется устройство вывода.
- **Ваша громкость остаётся вашей.** Общий ползунок Windows программа не двигает никогда.
- **Два ползунка** вместо конфигов и плагинов.
- **И поднимает, и убавляет.** Обычный ограничитель только срезает громкое, а тихий ролик так и остаётся тихим.

## Как это устроено

Ровно не пропускает звук через себя. Программа слушает общий звук системы (WASAPI loopback), меряет громкость по стандарту ITU-R BS.1770 (тот же LUFS, по которому нормализуют YouTube и ТВ) и плавно двигает громкость каждой играющей программы в микшере Windows (тот же ползунок, что в «Микшере громкости»). Общий регулятор Windows только читается и не меняется:

- поднимает не быстрее 3 дБ в секунду, без «качания»;
- убавляет до 10 дБ в секунду;
- если звук за 50 мс прыгнул выше цели больше чем на 5 дБ (взрыв, реклама, уведомление), режет сразу;
- в тишине и паузах ничего не трогает, поэтому шум не вытягивается.

Раз звук идёт мимо программы, у видео нет никакой задержки к картинке, а вылет программы не оставит вас без звука. Цена такого подхода: первые 20–30 мс резкого удара успевают пройти до того, как громкость сдвинется, а в «Микшере громкости» ползунки программ ходят сами. Свою громкость программы в микшере можно менять как обычно, Ровно запомнит её. Если программу закрыли посреди громкой сцены, Windows запомнит убавленную громкость, а Ровно при следующем запуске вернёт вашу.

## Замеры

Модель проверена на реальном звуке тем же кодом (`Rovno.exe --sim вход.wav выход.wav`), что работает в трее, с задержкой регулятора 20 мс. Громкость мерил ffmpeg (ebur128), скрипт `tests/measure.py`.

| Пример | Было | Стало |
|---|---|---|
| Фильм Tears of Steel, 12 мин: разброс кратковременной громкости (5–95%) | 23 дБ (−30…−7 LUFS) | 10 дБ (−26…−17 LUFS) |
| Тот же фильм, диапазон громкости LRA | 20,8 LU | 8,0 LU |
| Тихий ролик, потом громкий: разница между ними | 18 дБ (−26,3 и −8,3 LUFS) | 4 дБ (−22,0 и −17,9 LUFS) |

Живой тест на Windows 10 (версия 1.1): громкость Windows всё время стояла на 100% и не сдвинулась ни разу. Тихий отрывок (−26 LUFS) шёл с полной громкостью программы, громкий (−8 LUFS) программа убавила на 15 дБ, после выхода громкость программы в микшере вернулась на прежнюю.

## Сборка из исходников

Нужен только компилятор C#, который уже есть в Windows 10 и 11:

```
build.cmd
```

Весь код в одном файле `src/Rovno.cs`, без сторонних библиотек.

Служебные режимы: `Rovno.exe --sim in.wav out.wav [запас_дБ]` прогоняет файл через тот же алгоритм, `Rovno.exe --probe` печатает измеренную громкость и громкость Windows (она не должна меняться).

## Если что-то не так

- Windows может показать «Система Windows защитила ваш компьютер»: файл новый и без платной подписи. Нажмите «Подробнее» → «Выполнить в любом случае». Исходники открыты, можно собрать самому.
- У некоторых звуковых карт loopback идёт до регулятора громкости. Если звук начинает «гулять», добавьте строку `loopback=pre` в `%APPDATA%\Rovno\settings.ini` и перезапустите.
- Ошибки и идеи — в [Issues](https://github.com/Skeetcher111/rovno/issues).

Сделано в [MCPteam](https://mcpteam.ru) — сайты, приложения и программы на заказ. Лицензия MIT. Тестовый фильм — Tears of Steel, (CC) Blender Foundation, mango.blender.org.

---

<a name="english"></a>
# Rovno — even volume for Windows

**[Download for free (Rovno.exe, 37 KB)](https://github.com/Skeetcher111/rovno/releases/latest/download/Rovno.exe)**

One video is too quiet, the next one is deafening; a movie whispers and then explodes. Rovno keeps all system sound at about the same loudness: it lifts quiet content, lowers loud content and cuts sudden bangs at once. Works with every app.

- **No driver, no APO, no reboot.** A 34 KB tray app.
- **Zero added latency.** Audio never passes through Rovno: it measures the system mix (WASAPI loopback, ITU-R BS.1770 / LUFS) and rides the per-app volumes in the Windows Volume Mixer. The Windows master volume is never touched.
- Smooth: up at most 3 dB/s, down up to 10 dB/s, instant guard when loudness jumps 5 dB over the target within 50 ms. Silence is ignored, so noise is not pumped up.
- Follows the default output device (headphones, HDMI, Bluetooth). Restores your volume on exit.
- Set Windows and speaker volume as you like, Rovno leaves them alone. Normal content plays 6 dB under each app's mixer volume, quiet content gets those 6 dB back (`reserve=6` in settings.ini).

Measured with the same controller code (`--sim`): on the 12‑minute film *Tears of Steel* the short‑term loudness spread went from 23 dB to 10 dB (LRA 20.8 → 8.0 LU); a quiet clip followed by a loud one went from an 18 dB gap to 4 dB.

Trade-off: the first 20–30 ms of a sudden bang pass before the volume moves, and app sliders in the Volume Mixer move by themselves.

Build: run `build.cmd` (uses the C# compiler shipped with Windows). Single file, no dependencies. MIT license. Made by [MCPteam](https://mcpteam.ru).
