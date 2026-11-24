# AGENTS.md

## Проект
- Unity 6000.6.0f1, URP, Input System only (`activeInputHandler: 1`). UI — uGUI `Text` со шрифтом `Assets/UI/Finish/DejaVuSansMono.ttf`.
- Все скрипты в `Assembly-CSharp` / `Assembly-CSharp-Editor` (asmdef нет).
- Игровой цикл: `Assets/Scenes/MainMenu.unity` (build index 0) → гонки `1`–`4`.
  - Данные этапов (тексты, ключи, позиции на карте): `Assets/Resources/GameProgression.asset` (`GameProgressionManager`).
  - Гонка: `RaceDirector` (режим Race/Chase, отсчёт, прогресс по `WaypointPath`, победа/поражение) + `FinishLineTrigger` + `RaceFinishCanvas`.
  - Пауза/настройки: префаб `Assets/UI/Menu/TopSettingsMenu.prefab`, пауза через `GamePause`.

## Сборка сцен
- Editor: `Tools/RedLine/Собрать игровой цикл (меню, гонки, настройки)` (`Assets/Editor/RedLineGameSetup.cs`). Повторный запуск безопасен: существующие префабы и `MainMenu.unity` не перезаписываются (удали, чтобы пересоздать).
- Batchmode (Unity должен быть закрыт):
  `"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath . -executeMethod RedLineGameSetup.RunBatch -logFile /tmp/redline_setup.log`
- Арт меню генерируется `python3 Tools/MainMenu/generate_menu_art.py` (Pillow + numpy).

## Проверка компиляции без Unity
- Сгенерированные Unity `Assembly-CSharp*.csproj` (SDK-style) можно собрать `dotnet build` (нужен dotnet SDK); новые файлы, которых нет в csproj, добавлять во временный проект.
