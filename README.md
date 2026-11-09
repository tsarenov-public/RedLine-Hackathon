# RedLine-Hackathon — 3D-игра на Unity (URP)

публичный репозиторий команды хакатона. Всё про процесс — в `CONTRIBUTING.md`.

## Требования
- Unity: **[6000.6.0f1]** (шаблон Universal 3D / URP — не менять!)
- ОС: Windows 10 / macOS; Git-клиент: GitHub Desktop
- Windows: один раз выполнить `git config --global core.longpaths true`

## Как открыть проект
1. GitHub Desktop → Clone repository → выбрать `RedLine-Hackathon` (путь без пробелов/кириллицы/облачных синков).
2. Unity Hub → Installs → поставить версию из `ProjectSettings/ProjectVersion.txt`.
3. Unity Hub → Add → выбрать папку клона → Open, дождаться конца импорта.
4. Проверить: Edit → Project Settings → Editor = Visible Meta Files + Force Text.

## Схема веток
- `main` — рабочий билд; мержит только тимлид (PR beta → main), теги на билды.
- `beta` — интеграция команды; попадания только через PR.
- `feature/...`, `fix/...` — рабочие ветки от `beta`.
- Поток: `feature → beta → main`. Прямые пуши в beta/main запрещены правилами.

## Дневной цикл (шпаргалка)
1. Desktop: `beta` → Pull origin.
2. New Branch от beta: `feature/issue-XX-имя`.
3. Работа в Unity → коммит в Desktop (проверить галочки, `.meta` вместе с файлом) → Push.
4. Перед PR: `beta` → Pull → вернуться в ветку → Branch → Merge into current branch → beta → Push.
5. Create Pull Request (base = beta) → описание по шаблону → Squash and merge.
6. `beta` → Pull → удалить свою ветку → следующая issue.

## Коммиты
`тип: описание (#XX)` — feat / fix / art / audio / level / ui / chore.

## Владельцы и связи
- Облако с исходниками арта (PSD/.blend/WAV-мастера): [ССЫЛКА] — в git только game-ready.
- Владелец запекания света/навмеша: [ИМЯ]. Владелец билдов: [ИМЯ/лид].
- Доска задач: вкладка Projects.