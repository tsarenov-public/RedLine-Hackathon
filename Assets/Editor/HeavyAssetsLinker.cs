using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class HeavyAssetsLinker
{
    private struct HeavyAsset
    {
        public string RelativePath;
        public string DriveUrl;
    }

    private static readonly HeavyAsset[] RequiredAssets =
    {
        new HeavyAsset
        {
            RelativePath = "Assets/BuildingsAsset/Brooklyn/KB3D_Brooklyn-Native.fbx",
            DriveUrl = "https://drive.google.com/file/d/17zNl59I5ffsyV2TClJjRf7a0wPERNYD5/view?usp=drive_link"
        },
        // Пример добавления второго FBX:
        // new HeavyAsset
        // {
        //     RelativePath = "Assets/BuildingsAsset/Vehicles/Cars_Pack.fbx",
        //     DriveUrl = "ССЫЛКА_НА_ПАПКУ_ИЛИ_ФАЙЛ_GOOGLE_DRIVE"
        // }
    };

    static HeavyAssetsLinker()
    {
        EditorApplication.delayCall += () => CheckAndLinkAssets(silentIfOk: true);
    }

    [MenuItem("Tools/Link Heavy FBX Assets")]
    public static void ManualCheck()
    {
        CheckAndLinkAssets(silentIfOk: false);
    }

    private static void CheckAndLinkAssets(bool silentIfOk)
    {
        List<HeavyAsset> missing = RequiredAssets
            .Where(a => !File.Exists(Path.GetFullPath(a.RelativePath)))
            .ToList();

        if (missing.Count == 0)
        {
            if (!silentIfOk)
                EditorUtility.DisplayDialog("Всё на месте", "Все тяжелые FBX-файлы уже привязаны к проекту.", "ОК");
            return;
        }

        string fileNames = string.Join("\n• ", missing.Select(m => Path.GetFileName(m.RelativePath)));

        int choice = EditorUtility.DisplayDialogComplex(
            $"Не хватает тяжелых моделей ({missing.Count} шт.)",
            $"В проекте отсутствуют файлы:\n• {fileNames}\n\nУкажи папку на диске, куда скачал эти FBX (скрипт сам найдет их по именам)",
            "Выбрать папку с FBX",
            "Позже",
            "Открыть Google Диск"
        );

        if (choice == 2)
        {
            Application.OpenURL(missing[0].DriveUrl);
            return;
        }

        if (choice == 0)
        {
            string sourceFolder = EditorUtility.OpenFolderPanel("Выберите папку со скачанными FBX", "", "");
            if (string.IsNullOrEmpty(sourceFolder)) return;

            int linkedCount = 0;
            foreach (var asset in missing)
            {
                string fileName = Path.GetFileName(asset.RelativePath);
                string[] foundFiles = Directory.GetFiles(sourceFolder, fileName, SearchOption.AllDirectories);

                if (foundFiles.Length > 0)
                {
                    string fullSource = Path.GetFullPath(foundFiles[0]);
                    string fullTarget = Path.GetFullPath(asset.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullTarget));

                    if (CreateLinkOrCopy(fullSource, fullTarget))
                    {
                        linkedCount++;
                    }
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Готово", $"Успешно привязано моделей: {linkedCount} из {missing.Count}.", "ОК");
        }
    }

    private static bool CreateLinkOrCopy(string source, string target)
    {
        if (File.Exists(target))
            File.Delete(target);

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        RunProcess("ln", $"-s \"{source}\" \"{target}\"");
#elif UNITY_EDITOR_WIN
        // Пробуем создать жёсткую ссылку (работает мгновенно, если файлы на одном диске)
        RunProcess("cmd.exe", $"/c mklink /H \"{target}\" \"{source}\"");

        // Если файлы на разных дисках (например, C: и D:), mklink /H не сработает — копируем файл
        if (!File.Exists(target))
        {
            UnityEngine.Debug.Log($"[HeavyAssetsLinker] Файлы на разных дисках, копирую {Path.GetFileName(source)} в проект...");
            File.Copy(source, target, overwrite: true);
        }
#endif
        return File.Exists(target);
    }

    private static void RunProcess(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        process?.WaitForExit();
    }
}