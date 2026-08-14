using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class BlockFitLevelDesignerWindow : EditorWindow
{
    private const int MaxHolderSize = 12;
    private const int MaxPieceGridSize = 12;
    private const string DefaultLevelPrefabFolder = "Assets/_Game/Prefabs/_Level";

    [SerializeField] private LevelManager levelManager;
    [SerializeField] private LevelPrefab selectedLevelPrefab;
    [SerializeField] private string newLevelPrefabName = "Level_1";
    [SerializeField] private Vector2Int holderSize = new Vector2Int(7, 4);
    [SerializeField] private Vector2Int traySize = new Vector2Int(7, 3);
    [SerializeField] private BlockColorId paintColor = BlockColorId.Red;
    [SerializeField] private Vector2Int piecePainterSize = new Vector2Int(7, 3);
    [SerializeField] private List<HolderCellData> holderCells = new List<HolderCellData>();
    [SerializeField] private List<FixedHolderBlockData> fixedBlocks = new List<FixedHolderBlockData>();
    [SerializeField] private List<PieceWaveData> waves = new List<PieceWaveData>();

    private Vector2 scrollPosition;
    private string validationMessage = "No validation yet.";
    private int activeFixedBlockIndex = -1;
    private bool editFixedBlocksOnHolder;

    [MenuItem("BlockFit/Level Designer")]
    public static void Open()
    {
        GetWindow<BlockFitLevelDesignerWindow>("Level Designer");
    }

    private void OnEnable()
    {
        if (levelManager == null)
        {
            levelManager = FindFirstObjectByType<LevelManager>();
        }

        if (levelManager != null)
        {
            LoadFromLevelManager();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (levelManager == null)
        {
            EditorGUILayout.HelpBox("Select or assign a LevelManager in the scene.", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawLevelPrefabTools();
        DrawBoardSettings();
        DrawPaintPalette();
        DrawFixedBlocks();
        DrawHolderPainter();
        DrawWaves();
        DrawValidation();

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            LevelManager newLevelManager = (LevelManager)EditorGUILayout.ObjectField(levelManager, typeof(LevelManager), true, GUILayout.MinWidth(180));
            if (newLevelManager != levelManager)
            {
                levelManager = newLevelManager;
                if (levelManager != null)
                {
                    LoadFromLevelManager();
                }
            }

            if (GUILayout.Button("Use Selected", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                LevelManager selected = Selection.activeGameObject == null ? null : Selection.activeGameObject.GetComponent<LevelManager>();
                if (selected != null)
                {
                    levelManager = selected;
                    LoadFromLevelManager();
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(55)))
            {
                LoadFromLevelManager();
            }

            if (GUILayout.Button("Apply", EditorStyles.toolbarButton, GUILayout.Width(55)))
            {
                ApplyToLevelManager();
            }
        }
    }

    private void DrawLevelPrefabTools()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Level Prefab Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            selectedLevelPrefab = (LevelPrefab)EditorGUILayout.ObjectField("Level Prefab", selectedLevelPrefab, typeof(LevelPrefab), false);
            newLevelPrefabName = EditorGUILayout.TextField("New Prefab Name", newLevelPrefabName);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load Prefab", GUILayout.Height(28)))
                {
                    LoadFromLevelPrefab(selectedLevelPrefab);
                }

                if (GUILayout.Button("Save New Prefab", GUILayout.Height(28)))
                {
                    SaveNewLevelPrefab();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Over Prefab", GUILayout.Height(28)))
                {
                    SaveOverSelectedLevelPrefab();
                }

                if (GUILayout.Button("Add To LevelManager", GUILayout.Height(28)))
                {
                    AddSelectedLevelPrefabToLevelManager();
                }
            }
        }
    }

    private void DrawBoardSettings()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Board Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        holderSize = EditorGUILayout.Vector2IntField("Holder Size", holderSize);
        traySize = EditorGUILayout.Vector2IntField("Tray Size", traySize);
        piecePainterSize = EditorGUILayout.Vector2IntField("Piece Painter Size", piecePainterSize);

        holderSize.x = Mathf.Clamp(holderSize.x, 1, MaxHolderSize);
        holderSize.y = Mathf.Clamp(holderSize.y, 1, MaxHolderSize);
        traySize.x = Mathf.Clamp(traySize.x, 1, MaxHolderSize);
        traySize.y = Mathf.Clamp(traySize.y, 1, MaxHolderSize);
        piecePainterSize.x = Mathf.Clamp(piecePainterSize.x, 1, MaxPieceGridSize);
        piecePainterSize.y = Mathf.Clamp(piecePainterSize.y, 1, MaxPieceGridSize);

        if (EditorGUI.EndChangeCheck())
        {
            RemoveHolderCellsOutsideBoard();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Painter = Tray", GUILayout.Height(28)))
            {
                piecePainterSize = traySize;
            }

            if (GUILayout.Button("Trim Holder", GUILayout.Height(28)))
            {
                TrimHolderToPaintedCells();
            }

            if (GUILayout.Button("Create Sample Level", GUILayout.Height(28)))
            {
                CreateSampleLevel();
            }

            if (GUILayout.Button("Clear Level", GUILayout.Height(28)))
            {
                holderCells.Clear();
                fixedBlocks.Clear();
                waves.Clear();
                activeFixedBlockIndex = -1;
                editFixedBlocksOnHolder = false;
                validationMessage = "Level cleared.";
            }
        }
    }

    private void DrawPaintPalette()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Paint", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (BlockColorId colorId in GetPaintableColors())
            {
                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = colorId == BlockColorId.None ? Color.white : ColorFor(colorId);

                string label = colorId == paintColor ? $"[{ShortName(colorId)}]" : ShortName(colorId);
                if (GUILayout.Button(label, GUILayout.Width(48), GUILayout.Height(26)))
                {
                    paintColor = colorId;
                }

                GUI.backgroundColor = previousColor;
            }
        }
    }

    private void DrawHolderPainter()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("BoardHolder Painter", EditorStyles.boldLabel);

        if (IsEditingFixedBlock())
        {
            EditorGUILayout.HelpBox($"Editing fixed block: {fixedBlocks[activeFixedBlockIndex].id}. Click painted holder cells to add/remove them from this fixed block.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Click cells to paint target colors. Select None to erase cells and create custom holder shapes.", MessageType.None);
        }

        for (int y = holderSize.y - 1; y >= 0; y--)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(y.ToString(), GUILayout.Width(18));

                for (int x = 0; x < holderSize.x; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    DrawHolderCellButton(position);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(22);
            for (int x = 0; x < holderSize.x; x++)
            {
                GUILayout.Label(x.ToString(), GUILayout.Width(30));
            }
        }
    }

    private void DrawFixedBlocks()
    {
        EditorGUILayout.Space(10);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Fixed Holder Blocks", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Fixed Block", GUILayout.Width(110)))
            {
                fixedBlocks.Add(new FixedHolderBlockData { id = $"fixed_{fixedBlocks.Count + 1}" });
                activeFixedBlockIndex = fixedBlocks.Count - 1;
                editFixedBlocksOnHolder = true;
            }
        }

        EditorGUILayout.HelpBox("Fixed blocks are drawn directly on the holder. Select a fixed block, then click holder cells to mark where it starts locked.", MessageType.None);

        editFixedBlocksOnHolder = EditorGUILayout.Toggle("Edit Fixed On Holder", editFixedBlocksOnHolder);

        for (int fixedIndex = 0; fixedIndex < fixedBlocks.Count; fixedIndex++)
        {
            DrawFixedBlock(fixedIndex);
        }
    }

    private void DrawFixedBlock(int fixedIndex)
    {
        FixedHolderBlockData fixedBlock = fixedBlocks[fixedIndex];

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                fixedBlock.id = EditorGUILayout.TextField("Id", fixedBlock.id);

                string selectLabel = activeFixedBlockIndex == fixedIndex ? "Selected" : "Select";
                if (GUILayout.Button(selectLabel, GUILayout.Width(78)))
                {
                    activeFixedBlockIndex = fixedIndex;
                    editFixedBlocksOnHolder = true;
                }

                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    fixedBlock.cells.Clear();
                    fixedBlock.holderAnchor = Vector2Int.zero;
                }

                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    fixedBlocks.RemoveAt(fixedIndex);
                    if (activeFixedBlockIndex == fixedIndex)
                    {
                        activeFixedBlockIndex = -1;
                        editFixedBlocksOnHolder = false;
                    }
                    else if (activeFixedBlockIndex > fixedIndex)
                    {
                        activeFixedBlockIndex--;
                    }

                    return;
                }
            }

            EditorGUILayout.LabelField("Cells", fixedBlock.cells.Count.ToString());
        }
    }

    private void DrawWaves()
    {
        EditorGUILayout.Space(10);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Waves", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Wave", GUILayout.Width(80)))
            {
                waves.Add(new PieceWaveData());
            }
        }

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            PieceWaveData wave = waves[waveIndex];

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Wave {waveIndex + 1}", EditorStyles.boldLabel);

                    if (GUILayout.Button("+ Piece", GUILayout.Width(80)))
                    {
                        wave.pieces.Add(new LevelPieceData { id = $"wave{waveIndex + 1}_piece{wave.pieces.Count + 1}" });
                    }

                    if (GUILayout.Button("Remove Wave", GUILayout.Width(110)))
                    {
                        waves.RemoveAt(waveIndex);
                        break;
                    }
                }

                for (int pieceIndex = 0; pieceIndex < wave.pieces.Count; pieceIndex++)
                {
                    DrawPiece(wave, waveIndex, pieceIndex);
                }
            }
        }
    }

    private void DrawPiece(PieceWaveData wave, int waveIndex, int pieceIndex)
    {
        LevelPieceData piece = wave.pieces[pieceIndex];

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                piece.id = EditorGUILayout.TextField("Id", piece.id);

                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    wave.pieces.RemoveAt(pieceIndex);
                    return;
                }
            }

            piece.trayPosition = EditorGUILayout.Vector2IntField("Tray Anchor", piece.trayPosition);
            piece.trayPosition.x = Mathf.Clamp(piece.trayPosition.x, 0, Mathf.Max(0, traySize.x - 1));
            piece.trayPosition.y = Mathf.Clamp(piece.trayPosition.y, 0, Mathf.Max(0, traySize.y - 1));

            EditorGUILayout.LabelField($"Piece Painter - Wave {waveIndex + 1}, Piece {pieceIndex + 1}");
            for (int y = piecePainterSize.y - 1; y >= 0; y--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(18);
                    for (int x = 0; x < piecePainterSize.x; x++)
                    {
                        Vector2Int offset = new Vector2Int(x, y);
                        BlockColorId currentColor = GetPieceCellColor(piece, offset);
                        DrawPaintButton(currentColor, () => SetPieceCellColor(piece, offset, paintColor));
                    }
                }
            }
        }
    }

    private void DrawValidation()
    {
        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate", GUILayout.Height(28)))
            {
                validationMessage = ValidateLevel();
            }

            if (GUILayout.Button("Apply + Validate", GUILayout.Height(28)))
            {
                ApplyToLevelManager();
                validationMessage = ValidateLevel();
            }
        }

        EditorGUILayout.HelpBox(validationMessage, validationMessage.StartsWith("OK") ? MessageType.Info : MessageType.Warning);
    }

    private void DrawPaintButton(BlockColorId colorId, System.Action onClick)
    {
        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = colorId == BlockColorId.None ? new Color(0.24f, 0.24f, 0.24f) : ColorFor(colorId);

        if (GUILayout.Button(colorId == BlockColorId.None ? "." : ShortName(colorId), GUILayout.Width(30), GUILayout.Height(30)))
        {
            onClick?.Invoke();
        }

        GUI.backgroundColor = previousColor;
    }

    private void DrawHolderCellButton(Vector2Int position)
    {
        BlockColorId colorId = GetHolderColor(position);
        int fixedOwnerIndex = GetFixedBlockIndexAtHolderPosition(position);
        bool belongsToActiveFixedBlock = IsEditingFixedBlock() && fixedOwnerIndex == activeFixedBlockIndex;
        Color buttonColor = colorId == BlockColorId.None ? new Color(0.24f, 0.24f, 0.24f) : ColorFor(colorId);

        if (belongsToActiveFixedBlock)
        {
            buttonColor = Color.Lerp(buttonColor, Color.white, 0.45f);
        }

        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = buttonColor;

        string label = colorId == BlockColorId.None ? "." : ShortName(colorId);
        if (fixedOwnerIndex >= 0)
        {
            label = $"F{fixedOwnerIndex + 1}";
        }

        if (GUILayout.Button(label, GUILayout.Width(30), GUILayout.Height(30)))
        {
            if (IsEditingFixedBlock())
            {
                ToggleFixedCellOnHolder(fixedBlocks[activeFixedBlockIndex], position);
            }
            else
            {
                SetHolderColor(position, paintColor);
            }
        }

        GUI.backgroundColor = previousColor;
    }

    private void LoadFromLevelManager()
    {
        if (levelManager == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(levelManager);
        holderSize = serializedObject.FindProperty("holderSize").vector2IntValue;
        traySize = serializedObject.FindProperty("traySize").vector2IntValue;

        holderCells = ReadHolderCells(serializedObject.FindProperty("holderCells"));
        fixedBlocks = ReadFixedBlocks(serializedObject.FindProperty("fixedBlocks"));
        waves = ReadWaves(serializedObject.FindProperty("waves"));
        ClampActiveFixedBlockIndex();
        validationMessage = "Loaded from LevelManager.";
    }

    private void ApplyToLevelManager()
    {
        if (levelManager == null)
        {
            return;
        }

        Undo.RecordObject(levelManager, "Apply BlockFit Level");

        SerializedObject serializedObject = new SerializedObject(levelManager);
        serializedObject.FindProperty("holderSize").vector2IntValue = holderSize;
        serializedObject.FindProperty("traySize").vector2IntValue = traySize;
        WriteHolderCells(serializedObject.FindProperty("holderCells"), holderCells);
        WriteFixedBlocks(serializedObject.FindProperty("fixedBlocks"), fixedBlocks);
        WriteWaves(serializedObject.FindProperty("waves"), waves);
        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(levelManager);
        validationMessage = "Applied to LevelManager.";
    }

    private void LoadFromLevelPrefab(LevelPrefab levelPrefab)
    {
        LevelPrefab prefabAsset = GetLevelPrefabAsset(levelPrefab);
        if (prefabAsset == null)
        {
            validationMessage = "Select a LevelPrefab asset first.";
            return;
        }

        holderSize = prefabAsset.HolderSize;
        traySize = prefabAsset.TraySize;
        piecePainterSize = traySize;
        holderCells = LevelDataUtility.CloneHolderCells(prefabAsset.HolderCells);
        fixedBlocks = LevelDataUtility.CloneFixedBlocks(prefabAsset.FixedBlocks);
        waves = LevelDataUtility.CloneWaves(prefabAsset.Waves);
        selectedLevelPrefab = prefabAsset;
        newLevelPrefabName = string.IsNullOrWhiteSpace(prefabAsset.LevelId) ? prefabAsset.name : prefabAsset.LevelId;
        ClampActiveFixedBlockIndex();
        validationMessage = $"Loaded level prefab: {prefabAsset.name}.";
    }

    private void SaveNewLevelPrefab()
    {
        string validation = ValidateLevel();
        if (!validation.StartsWith("OK"))
        {
            validationMessage = validation;
            return;
        }

        EnsureLevelPrefabFolder();
        string safeName = SanitizeFileName(newLevelPrefabName);
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Level Prefab",
            safeName,
            "prefab",
            "Choose where to save this BlockFit level prefab.",
            DefaultLevelPrefabFolder);

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        selectedLevelPrefab = CreateLevelPrefabAsset(path);
        AddSelectedLevelPrefabToLevelManager();
        validationMessage = $"Saved level prefab: {path}.";
    }

    private void SaveOverSelectedLevelPrefab()
    {
        string validation = ValidateLevel();
        if (!validation.StartsWith("OK"))
        {
            validationMessage = validation;
            return;
        }

        LevelPrefab prefabAsset = GetLevelPrefabAsset(selectedLevelPrefab);
        if (prefabAsset == null)
        {
            validationMessage = "Select a LevelPrefab asset before saving over it.";
            return;
        }

        Undo.RecordObject(prefabAsset, "Save BlockFit Level Prefab");
        prefabAsset.SetData(newLevelPrefabName, holderSize, traySize, holderCells, fixedBlocks, waves);
        EditorUtility.SetDirty(prefabAsset);
        AssetDatabase.SaveAssets();
        validationMessage = $"Saved over level prefab: {AssetDatabase.GetAssetPath(prefabAsset)}.";
    }

    private LevelPrefab CreateLevelPrefabAsset(string path)
    {
        GameObject levelObject = new GameObject(Path.GetFileNameWithoutExtension(path));
        LevelPrefab levelPrefab = levelObject.AddComponent<LevelPrefab>();
        levelPrefab.SetData(newLevelPrefabName, holderSize, traySize, holderCells, fixedBlocks, waves);

        GameObject prefabObject = PrefabUtility.SaveAsPrefabAsset(levelObject, path);
        DestroyImmediate(levelObject);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return prefabObject == null ? null : prefabObject.GetComponent<LevelPrefab>();
    }

    private void AddSelectedLevelPrefabToLevelManager()
    {
        LevelPrefab prefabAsset = GetLevelPrefabAsset(selectedLevelPrefab);
        if (levelManager == null || prefabAsset == null)
        {
            validationMessage = "Assign LevelManager and select a LevelPrefab first.";
            return;
        }

        SerializedObject serializedObject = new SerializedObject(levelManager);
        SerializedProperty levelPrefabsProperty = serializedObject.FindProperty("levelPrefabs");

        for (int i = 0; i < levelPrefabsProperty.arraySize; i++)
        {
            if (levelPrefabsProperty.GetArrayElementAtIndex(i).objectReferenceValue == prefabAsset)
            {
                validationMessage = $"{prefabAsset.name} is already in LevelManager.";
                return;
            }
        }

        Undo.RecordObject(levelManager, "Add BlockFit Level Prefab");
        int newIndex = levelPrefabsProperty.arraySize;
        levelPrefabsProperty.InsertArrayElementAtIndex(newIndex);
        levelPrefabsProperty.GetArrayElementAtIndex(newIndex).objectReferenceValue = prefabAsset;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(levelManager);
        validationMessage = $"Added {prefabAsset.name} to LevelManager.";
    }

    private LevelPrefab GetLevelPrefabAsset(LevelPrefab levelPrefab)
    {
        if (levelPrefab == null)
        {
            return null;
        }

        LevelPrefab prefabAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(levelPrefab);
        if (prefabAsset != null)
        {
            return prefabAsset;
        }

        string path = AssetDatabase.GetAssetPath(levelPrefab);
        return string.IsNullOrEmpty(path) ? null : levelPrefab;
    }

    private void EnsureLevelPrefabFolder()
    {
        if (AssetDatabase.IsValidFolder(DefaultLevelPrefabFolder))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/_Game/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/_Game", "Prefabs");
        }

        AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "Levels");
    }

    private string SanitizeFileName(string fileName)
    {
        string safeName = string.IsNullOrWhiteSpace(fileName) ? "Level" : fileName;
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidChar, '_');
        }

        return safeName;
    }

    private void CreateSampleLevel()
    {
        holderSize = new Vector2Int(7, 4);
        traySize = new Vector2Int(7, 3);
        piecePainterSize = traySize;
        holderCells.Clear();
        fixedBlocks.Clear();
        waves.Clear();
        activeFixedBlockIndex = -1;
        editFixedBlocksOnHolder = false;

        AddHolderPiece(new Vector2Int(0, 0), BlockColorId.Red, new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 0) });
        AddHolderPiece(new Vector2Int(2, 0), BlockColorId.Blue, new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(2, 0) });
        AddHolderPiece(new Vector2Int(5, 0), BlockColorId.Yellow, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) });
        AddHolderPiece(new Vector2Int(2, 2), BlockColorId.Green, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1) });
        AddHolderPiece(new Vector2Int(5, 2), BlockColorId.Orange, new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) });

        PieceWaveData wave1 = new PieceWaveData();
        wave1.pieces.Add(CreatePiece("red_l", new Vector2Int(0, 0), BlockColorId.Red, new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 0) }));
        wave1.pieces.Add(CreatePiece("blue_z", new Vector2Int(2, 0), BlockColorId.Blue, new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(2, 0) }));
        wave1.pieces.Add(CreatePiece("yellow_square", new Vector2Int(5, 0), BlockColorId.Yellow, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) }));

        PieceWaveData wave2 = new PieceWaveData();
        wave2.pieces.Add(CreatePiece("green_t", new Vector2Int(1, 0), BlockColorId.Green, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1) }));
        wave2.pieces.Add(CreatePiece("orange_corner", new Vector2Int(5, 0), BlockColorId.Orange, new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) }));

        waves.Add(wave1);
        waves.Add(wave2);
        validationMessage = ValidateLevel();
    }

    private string ValidateLevel()
    {
        Dictionary<BlockColorId, int> holderCounts = CountHolderCells();
        Dictionary<BlockColorId, int> pieceCounts = CountPieceCells();
        List<string> errors = new List<string>();

        if (holderCells.Count == 0)
        {
            errors.Add("BoardHolder has no cells.");
        }

        if (waves.Count == 0)
        {
            errors.Add("Level has no waves.");
        }

        foreach (BlockColorId colorId in GetPaintableColors())
        {
            if (colorId == BlockColorId.None)
            {
                continue;
            }

            holderCounts.TryGetValue(colorId, out int holderCount);
            pieceCounts.TryGetValue(colorId, out int pieceCount);

            if (holderCount != pieceCount)
            {
                errors.Add($"{colorId}: holder has {holderCount}, pieces have {pieceCount}.");
            }
        }

        ValidateFixedBlocks(errors);

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            PieceWaveData wave = waves[waveIndex];
            if (wave.pieces.Count == 0)
            {
                errors.Add($"Wave {waveIndex + 1} has no pieces.");
            }

            for (int pieceIndex = 0; pieceIndex < wave.pieces.Count; pieceIndex++)
            {
                LevelPieceData piece = wave.pieces[pieceIndex];
                if (piece.cells.Count == 0)
                {
                    errors.Add($"Wave {waveIndex + 1}, piece {pieceIndex + 1} has no cells.");
                }

                foreach (ColoredCellData cell in piece.cells)
                {
                    Vector2Int trayPosition = piece.trayPosition + cell.offset;
                    if (trayPosition.x < 0 || trayPosition.y < 0 || trayPosition.x >= traySize.x || trayPosition.y >= traySize.y)
                    {
                        errors.Add($"{piece.id} goes outside tray at {trayPosition}.");
                        break;
                    }
                }
            }
        }

        return errors.Count == 0 ? $"OK - {holderCells.Count} holder cells, {fixedBlocks.Count} fixed blocks, {waves.Count} waves." : string.Join("\n", errors);
    }

    private void ValidateFixedBlocks(List<string> errors)
    {
        Dictionary<Vector2Int, string> occupiedPositions = new Dictionary<Vector2Int, string>();

        for (int fixedIndex = 0; fixedIndex < fixedBlocks.Count; fixedIndex++)
        {
            FixedHolderBlockData fixedBlock = fixedBlocks[fixedIndex];
            string blockName = string.IsNullOrWhiteSpace(fixedBlock.id) ? $"Fixed block {fixedIndex + 1}" : fixedBlock.id;

            if (fixedBlock.cells.Count == 0)
            {
                errors.Add($"{blockName} has no cells.");
                continue;
            }

            foreach (ColoredCellData cell in fixedBlock.cells)
            {
                Vector2Int holderPosition = fixedBlock.holderAnchor + cell.offset;
                BlockColorId holderColor = GetHolderColor(holderPosition);

                if (holderColor == BlockColorId.None)
                {
                    errors.Add($"{blockName} has a cell outside painted holder at {holderPosition}.");
                    continue;
                }

                if (holderColor != cell.color)
                {
                    errors.Add($"{blockName} needs {cell.color} at {holderPosition}, but holder is {holderColor}.");
                }

                if (occupiedPositions.TryGetValue(holderPosition, out string occupyingBlock))
                {
                    errors.Add($"{blockName} overlaps {occupyingBlock} at {holderPosition}.");
                }
                else
                {
                    occupiedPositions[holderPosition] = blockName;
                }
            }
        }
    }

    private LevelPieceData CreatePiece(string id, Vector2Int trayPosition, BlockColorId colorId, IEnumerable<Vector2Int> offsets)
    {
        LevelPieceData piece = new LevelPieceData
        {
            id = id,
            trayPosition = trayPosition
        };

        foreach (Vector2Int offset in offsets)
        {
            piece.cells.Add(new ColoredCellData { offset = offset, color = colorId });
        }

        return piece;
    }

    private void AddHolderPiece(Vector2Int anchor, BlockColorId colorId, IEnumerable<Vector2Int> offsets)
    {
        foreach (Vector2Int offset in offsets)
        {
            SetHolderColor(anchor + offset, colorId);
        }
    }

    private BlockColorId GetHolderColor(Vector2Int position)
    {
        foreach (HolderCellData cell in holderCells)
        {
            if (cell.position == position)
            {
                return cell.color;
            }
        }

        return BlockColorId.None;
    }

    private void SetHolderColor(Vector2Int position, BlockColorId colorId)
    {
        for (int i = holderCells.Count - 1; i >= 0; i--)
        {
            if (holderCells[i].position == position)
            {
                if (colorId == BlockColorId.None)
                {
                    holderCells.RemoveAt(i);
                    RemoveFixedCellsAtHolderPosition(position);
                }
                else
                {
                    holderCells[i].color = colorId;
                    UpdateFixedCellsAtHolderPosition(position, colorId);
                }

                return;
            }
        }

        if (colorId != BlockColorId.None)
        {
            holderCells.Add(new HolderCellData { position = position, color = colorId });
            UpdateFixedCellsAtHolderPosition(position, colorId);
        }
    }

    private BlockColorId GetPieceCellColor(LevelPieceData piece, Vector2Int offset)
    {
        foreach (ColoredCellData cell in piece.cells)
        {
            if (cell.offset == offset)
            {
                return cell.color;
            }
        }

        return BlockColorId.None;
    }

    private void SetPieceCellColor(LevelPieceData piece, Vector2Int offset, BlockColorId colorId)
    {
        for (int i = piece.cells.Count - 1; i >= 0; i--)
        {
            if (piece.cells[i].offset == offset)
            {
                if (colorId == BlockColorId.None)
                {
                    piece.cells.RemoveAt(i);
                }
                else
                {
                    ColoredCellData cell = piece.cells[i];
                    cell.color = colorId;
                    piece.cells[i] = cell;
                }

                return;
            }
        }

        if (colorId != BlockColorId.None)
        {
            piece.cells.Add(new ColoredCellData { offset = offset, color = colorId });
        }
    }

    private bool IsEditingFixedBlock()
    {
        return editFixedBlocksOnHolder && activeFixedBlockIndex >= 0 && activeFixedBlockIndex < fixedBlocks.Count;
    }

    private void ClampActiveFixedBlockIndex()
    {
        if (fixedBlocks.Count == 0)
        {
            activeFixedBlockIndex = -1;
            editFixedBlocksOnHolder = false;
            return;
        }

        activeFixedBlockIndex = Mathf.Clamp(activeFixedBlockIndex, -1, fixedBlocks.Count - 1);
    }

    private int GetFixedBlockIndexAtHolderPosition(Vector2Int holderPosition)
    {
        for (int fixedIndex = 0; fixedIndex < fixedBlocks.Count; fixedIndex++)
        {
            if (GetFixedCellIndexAtHolderPosition(fixedBlocks[fixedIndex], holderPosition) >= 0)
            {
                return fixedIndex;
            }
        }

        return -1;
    }

    private int GetFixedCellIndexAtHolderPosition(FixedHolderBlockData fixedBlock, Vector2Int holderPosition)
    {
        for (int i = 0; i < fixedBlock.cells.Count; i++)
        {
            if (fixedBlock.holderAnchor + fixedBlock.cells[i].offset == holderPosition)
            {
                return i;
            }
        }

        return -1;
    }

    private void ToggleFixedCellOnHolder(FixedHolderBlockData fixedBlock, Vector2Int holderPosition)
    {
        BlockColorId holderColor = GetHolderColor(holderPosition);
        if (holderColor == BlockColorId.None)
        {
            validationMessage = "Paint this holder cell before marking it as fixed.";
            return;
        }

        int ownerIndex = GetFixedBlockIndexAtHolderPosition(holderPosition);
        if (ownerIndex >= 0 && ownerIndex != activeFixedBlockIndex)
        {
            validationMessage = $"This holder cell already belongs to {fixedBlocks[ownerIndex].id}.";
            return;
        }

        int existingCellIndex = GetFixedCellIndexAtHolderPosition(fixedBlock, holderPosition);
        if (existingCellIndex >= 0)
        {
            fixedBlock.cells.RemoveAt(existingCellIndex);
        }
        else
        {
            fixedBlock.cells.Add(new ColoredCellData
            {
                offset = holderPosition - fixedBlock.holderAnchor,
                color = holderColor
            });
        }

        NormalizeFixedBlock(fixedBlock);
    }

    private void NormalizeFixedBlock(FixedHolderBlockData fixedBlock)
    {
        if (fixedBlock.cells.Count == 0)
        {
            fixedBlock.holderAnchor = Vector2Int.zero;
            return;
        }

        Vector2Int oldAnchor = fixedBlock.holderAnchor;
        Vector2Int min = oldAnchor + fixedBlock.cells[0].offset;

        foreach (ColoredCellData cell in fixedBlock.cells)
        {
            Vector2Int holderPosition = oldAnchor + cell.offset;
            min.x = Mathf.Min(min.x, holderPosition.x);
            min.y = Mathf.Min(min.y, holderPosition.y);
        }

        for (int i = 0; i < fixedBlock.cells.Count; i++)
        {
            ColoredCellData cell = fixedBlock.cells[i];
            Vector2Int holderPosition = oldAnchor + cell.offset;
            BlockColorId holderColor = GetHolderColor(holderPosition);
            cell.offset = holderPosition - min;
            cell.color = holderColor == BlockColorId.None ? cell.color : holderColor;
            fixedBlock.cells[i] = cell;
        }

        fixedBlock.holderAnchor = min;
    }

    private void RemoveFixedCellsAtHolderPosition(Vector2Int holderPosition)
    {
        foreach (FixedHolderBlockData fixedBlock in fixedBlocks)
        {
            int cellIndex = GetFixedCellIndexAtHolderPosition(fixedBlock, holderPosition);
            if (cellIndex >= 0)
            {
                fixedBlock.cells.RemoveAt(cellIndex);
                NormalizeFixedBlock(fixedBlock);
            }
        }
    }

    private void UpdateFixedCellsAtHolderPosition(Vector2Int holderPosition, BlockColorId colorId)
    {
        foreach (FixedHolderBlockData fixedBlock in fixedBlocks)
        {
            int cellIndex = GetFixedCellIndexAtHolderPosition(fixedBlock, holderPosition);
            if (cellIndex < 0)
            {
                continue;
            }

            ColoredCellData cell = fixedBlock.cells[cellIndex];
            cell.color = colorId;
            fixedBlock.cells[cellIndex] = cell;
        }
    }

    private void RemoveHolderCellsOutsideBoard()
    {
        for (int i = holderCells.Count - 1; i >= 0; i--)
        {
            Vector2Int position = holderCells[i].position;
            if (position.x < 0 || position.y < 0 || position.x >= holderSize.x || position.y >= holderSize.y)
            {
                holderCells.RemoveAt(i);
            }
        }

        RemoveFixedCellsOutsideHolderBoard();
    }

    private void RemoveFixedCellsOutsideHolderBoard()
    {
        foreach (FixedHolderBlockData fixedBlock in fixedBlocks)
        {
            for (int i = fixedBlock.cells.Count - 1; i >= 0; i--)
            {
                Vector2Int holderPosition = fixedBlock.holderAnchor + fixedBlock.cells[i].offset;
                if (holderPosition.x < 0 || holderPosition.y < 0 || holderPosition.x >= holderSize.x || holderPosition.y >= holderSize.y)
                {
                    fixedBlock.cells.RemoveAt(i);
                }
            }

            NormalizeFixedBlock(fixedBlock);
        }
    }

    private void TrimHolderToPaintedCells()
    {
        if (holderCells.Count == 0)
        {
            validationMessage = "Cannot trim holder because it has no painted cells.";
            return;
        }

        Vector2Int min = holderCells[0].position;
        Vector2Int max = holderCells[0].position;

        foreach (HolderCellData cell in holderCells)
        {
            min.x = Mathf.Min(min.x, cell.position.x);
            min.y = Mathf.Min(min.y, cell.position.y);
            max.x = Mathf.Max(max.x, cell.position.x);
            max.y = Mathf.Max(max.y, cell.position.y);
        }

        foreach (HolderCellData cell in holderCells)
        {
            cell.position -= min;
        }

        foreach (FixedHolderBlockData fixedBlock in fixedBlocks)
        {
            fixedBlock.holderAnchor -= min;
        }

        holderSize = new Vector2Int(max.x - min.x + 1, max.y - min.y + 1);
        validationMessage = $"Holder trimmed to {holderSize.x}x{holderSize.y}.";
    }

    private Dictionary<BlockColorId, int> CountHolderCells()
    {
        Dictionary<BlockColorId, int> counts = new Dictionary<BlockColorId, int>();
        foreach (HolderCellData cell in holderCells)
        {
            AddCount(counts, cell.color);
        }

        return counts;
    }

    private Dictionary<BlockColorId, int> CountPieceCells()
    {
        Dictionary<BlockColorId, int> counts = new Dictionary<BlockColorId, int>();
        foreach (FixedHolderBlockData fixedBlock in fixedBlocks)
        {
            foreach (ColoredCellData cell in fixedBlock.cells)
            {
                AddCount(counts, cell.color);
            }
        }

        foreach (PieceWaveData wave in waves)
        {
            foreach (LevelPieceData piece in wave.pieces)
            {
                foreach (ColoredCellData cell in piece.cells)
                {
                    AddCount(counts, cell.color);
                }
            }
        }

        return counts;
    }

    private void AddCount(Dictionary<BlockColorId, int> counts, BlockColorId colorId)
    {
        if (!counts.ContainsKey(colorId))
        {
            counts[colorId] = 0;
        }

        counts[colorId]++;
    }

    private BlockColorId[] GetPaintableColors()
    {
        return new[]
        {
            BlockColorId.None,
            BlockColorId.Red,
            BlockColorId.Orange,
            BlockColorId.Yellow,
            BlockColorId.Green,
            BlockColorId.Blue,
            BlockColorId.Purple,
            BlockColorId.Brown,
            BlockColorId.Gray
        };
    }

    private string ShortName(BlockColorId colorId)
    {
        switch (colorId)
        {
            case BlockColorId.None:
                return "None";
            case BlockColorId.Red:
                return "R";
            case BlockColorId.Orange:
                return "O";
            case BlockColorId.Yellow:
                return "Y";
            case BlockColorId.Green:
                return "G";
            case BlockColorId.Blue:
                return "B";
            case BlockColorId.Purple:
                return "P";
            case BlockColorId.Brown:
                return "Br";
            case BlockColorId.Gray:
                return "Gr";
            default:
                return "?";
        }
    }

    private Color ColorFor(BlockColorId colorId)
    {
        switch (colorId)
        {
            case BlockColorId.Red:
                return new Color(0.86f, 0.16f, 0.05f);
            case BlockColorId.Orange:
                return new Color(1f, 0.48f, 0.02f);
            case BlockColorId.Yellow:
                return new Color(1f, 0.82f, 0.05f);
            case BlockColorId.Green:
                return new Color(0.02f, 0.72f, 0.22f);
            case BlockColorId.Blue:
                return new Color(0.02f, 0.36f, 0.95f);
            case BlockColorId.Purple:
                return new Color(0.52f, 0.26f, 0.86f);
            case BlockColorId.Brown:
                return new Color(0.48f, 0.22f, 0.02f);
            case BlockColorId.Gray:
                return new Color(0.42f, 0.45f, 0.50f);
            default:
                return Color.white;
        }
    }

    private List<HolderCellData> ReadHolderCells(SerializedProperty property)
    {
        List<HolderCellData> result = new List<HolderCellData>();
        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty item = property.GetArrayElementAtIndex(i);
            result.Add(new HolderCellData
            {
                position = item.FindPropertyRelative("position").vector2IntValue,
                color = (BlockColorId)item.FindPropertyRelative("color").enumValueIndex
            });
        }

        return result;
    }

    private List<PieceWaveData> ReadWaves(SerializedProperty property)
    {
        List<PieceWaveData> result = new List<PieceWaveData>();
        for (int waveIndex = 0; waveIndex < property.arraySize; waveIndex++)
        {
            SerializedProperty waveProperty = property.GetArrayElementAtIndex(waveIndex);
            SerializedProperty piecesProperty = waveProperty.FindPropertyRelative("pieces");
            PieceWaveData wave = new PieceWaveData();

            for (int pieceIndex = 0; pieceIndex < piecesProperty.arraySize; pieceIndex++)
            {
                SerializedProperty pieceProperty = piecesProperty.GetArrayElementAtIndex(pieceIndex);
                LevelPieceData piece = new LevelPieceData
                {
                    id = pieceProperty.FindPropertyRelative("id").stringValue,
                    trayPosition = pieceProperty.FindPropertyRelative("trayPosition").vector2IntValue
                };

                SerializedProperty cellsProperty = pieceProperty.FindPropertyRelative("cells");
                for (int cellIndex = 0; cellIndex < cellsProperty.arraySize; cellIndex++)
                {
                    SerializedProperty cellProperty = cellsProperty.GetArrayElementAtIndex(cellIndex);
                    piece.cells.Add(new ColoredCellData
                    {
                        offset = cellProperty.FindPropertyRelative("offset").vector2IntValue,
                        color = (BlockColorId)cellProperty.FindPropertyRelative("color").enumValueIndex
                    });
                }

                wave.pieces.Add(piece);
            }

            result.Add(wave);
        }

        return result;
    }

    private List<FixedHolderBlockData> ReadFixedBlocks(SerializedProperty property)
    {
        List<FixedHolderBlockData> result = new List<FixedHolderBlockData>();
        for (int fixedIndex = 0; fixedIndex < property.arraySize; fixedIndex++)
        {
            SerializedProperty fixedProperty = property.GetArrayElementAtIndex(fixedIndex);
            FixedHolderBlockData fixedBlock = new FixedHolderBlockData
            {
                id = fixedProperty.FindPropertyRelative("id").stringValue,
                holderAnchor = fixedProperty.FindPropertyRelative("holderAnchor").vector2IntValue
            };

            SerializedProperty cellsProperty = fixedProperty.FindPropertyRelative("cells");
            for (int cellIndex = 0; cellIndex < cellsProperty.arraySize; cellIndex++)
            {
                SerializedProperty cellProperty = cellsProperty.GetArrayElementAtIndex(cellIndex);
                fixedBlock.cells.Add(new ColoredCellData
                {
                    offset = cellProperty.FindPropertyRelative("offset").vector2IntValue,
                    color = (BlockColorId)cellProperty.FindPropertyRelative("color").enumValueIndex
                });
            }

            result.Add(fixedBlock);
        }

        return result;
    }

    private void WriteHolderCells(SerializedProperty property, List<HolderCellData> cells)
    {
        property.ClearArray();
        for (int i = 0; i < cells.Count; i++)
        {
            property.InsertArrayElementAtIndex(i);
            SerializedProperty item = property.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("position").vector2IntValue = cells[i].position;
            item.FindPropertyRelative("color").enumValueIndex = (int)cells[i].color;
        }
    }

    private void WriteFixedBlocks(SerializedProperty property, List<FixedHolderBlockData> levelFixedBlocks)
    {
        property.ClearArray();
        for (int fixedIndex = 0; fixedIndex < levelFixedBlocks.Count; fixedIndex++)
        {
            property.InsertArrayElementAtIndex(fixedIndex);
            SerializedProperty fixedProperty = property.GetArrayElementAtIndex(fixedIndex);
            FixedHolderBlockData fixedBlock = levelFixedBlocks[fixedIndex];
            fixedProperty.FindPropertyRelative("id").stringValue = fixedBlock.id;
            fixedProperty.FindPropertyRelative("holderAnchor").vector2IntValue = fixedBlock.holderAnchor;

            SerializedProperty cellsProperty = fixedProperty.FindPropertyRelative("cells");
            cellsProperty.ClearArray();
            for (int cellIndex = 0; cellIndex < fixedBlock.cells.Count; cellIndex++)
            {
                cellsProperty.InsertArrayElementAtIndex(cellIndex);
                SerializedProperty cellProperty = cellsProperty.GetArrayElementAtIndex(cellIndex);
                cellProperty.FindPropertyRelative("offset").vector2IntValue = fixedBlock.cells[cellIndex].offset;
                cellProperty.FindPropertyRelative("color").enumValueIndex = (int)fixedBlock.cells[cellIndex].color;
            }
        }
    }

    private void WriteWaves(SerializedProperty property, List<PieceWaveData> levelWaves)
    {
        property.ClearArray();
        for (int waveIndex = 0; waveIndex < levelWaves.Count; waveIndex++)
        {
            property.InsertArrayElementAtIndex(waveIndex);
            SerializedProperty waveProperty = property.GetArrayElementAtIndex(waveIndex);
            SerializedProperty piecesProperty = waveProperty.FindPropertyRelative("pieces");
            piecesProperty.ClearArray();

            PieceWaveData wave = levelWaves[waveIndex];
            for (int pieceIndex = 0; pieceIndex < wave.pieces.Count; pieceIndex++)
            {
                piecesProperty.InsertArrayElementAtIndex(pieceIndex);
                SerializedProperty pieceProperty = piecesProperty.GetArrayElementAtIndex(pieceIndex);
                LevelPieceData piece = wave.pieces[pieceIndex];
                pieceProperty.FindPropertyRelative("id").stringValue = piece.id;
                pieceProperty.FindPropertyRelative("trayPosition").vector2IntValue = piece.trayPosition;

                SerializedProperty cellsProperty = pieceProperty.FindPropertyRelative("cells");
                cellsProperty.ClearArray();
                for (int cellIndex = 0; cellIndex < piece.cells.Count; cellIndex++)
                {
                    cellsProperty.InsertArrayElementAtIndex(cellIndex);
                    SerializedProperty cellProperty = cellsProperty.GetArrayElementAtIndex(cellIndex);
                    cellProperty.FindPropertyRelative("offset").vector2IntValue = piece.cells[cellIndex].offset;
                    cellProperty.FindPropertyRelative("color").enumValueIndex = (int)piece.cells[cellIndex].color;
                }
            }
        }
    }
}
