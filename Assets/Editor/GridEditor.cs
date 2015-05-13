using UnityEngine;
using UnityEditor;

using System.Collections;
using System.IO;

[CustomEditor(typeof(Grid))]
public class GridEditor : Editor {

	Grid grid;

	private int oldIndex = 0;

	private Vector3 mouseBeginPos;
	private Vector3 mouseEndPos;

	void OnEnable(){
		grid = (Grid)target;
	}

	[MenuItem("Assets/Create/TileSet")]
	static void CreateTileSet(){
		var asset = ScriptableObject.CreateInstance<TileSet>();
		var path = AssetDatabase.GetAssetPath(Selection.activeObject);

		if(string.IsNullOrEmpty(path)){
			path = "Assets";
		}else if(Path.GetExtension(path) != ""){
			path = path.Replace (Path.GetFileName(path),"");
		}else{
			path+="/";
		}

		var assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "TileSet.asset");
		AssetDatabase.CreateAsset(asset,assetPathAndName);
		AssetDatabase.SaveAssets();
		EditorUtility.FocusProjectWindow();
		Selection.activeObject = asset;
		asset.hideFlags = HideFlags.DontSave;
	}

	public override void OnInspectorGUI(){
		//base.OnInspectorGUI();

		grid.width = createSlider("Width",grid.width);
		grid.height = createSlider("Height", grid.height);

		if(GUILayout.Button("Open Grid Window")){
			GridWindow window = (GridWindow)EditorWindow.GetWindow (typeof(GridWindow));
			window.init();
		}

		// Tile Prefab
		EditorGUI.BeginChangeCheck();
		var newTilePrefab = (Transform)EditorGUILayout.ObjectField("Tile Prefab",grid.tilePrefab,typeof(Transform),false);
		if(EditorGUI.EndChangeCheck()){
			grid.tilePrefab = newTilePrefab;
			Undo.RecordObject(target,"Grid Changed");
		}

		//Tile Map
		EditorGUI.BeginChangeCheck();
		var newTileSet = (TileSet) EditorGUILayout.ObjectField("Tileset", grid.tileSet,typeof(TileSet),false);
		if(EditorGUI.EndChangeCheck()){
			grid.tileSet = newTileSet;
			Undo.RecordObject(target,"Grid Changed");
		}

		if(grid.tileSet != null){
			EditorGUI.BeginChangeCheck();
			var names = new string[grid.tileSet.prefabs.Length];
			var values = new int[names.Length];

			for(int i = 0; i < names.Length;i++){
				names[i] = grid.tileSet.prefabs[i] != null ? grid.tileSet.prefabs[i].name : "";
				values[i] = i;
			}

			var index = EditorGUILayout.IntPopup("Select Tile",oldIndex,names,values);

			if(EditorGUI.EndChangeCheck()){
				Undo.RecordObject(target,"Grid Changed");
				if(oldIndex != index){
					oldIndex = index;
					grid.tilePrefab = grid.tileSet.prefabs[index];

					float width = grid.tilePrefab.GetComponent<Renderer>().bounds.size.x;
					float height = grid.tilePrefab.GetComponent<Renderer>().bounds.size.y;

					grid.width = width; 
					grid.height = height;

				}
			}
		}

		EditorGUI.BeginChangeCheck();

		bool draggable = EditorGUILayout.Toggle ("Toggle Dragging: ", grid.draggable);
		if(EditorGUI.EndChangeCheck()){
			grid.draggable = draggable;
		}
	}

	private float createSlider(string labelName, float sliderPosition){
		GUILayout.BeginHorizontal();
		GUILayout.Label ("Grid " + labelName);
		sliderPosition = EditorGUILayout.Slider(sliderPosition,1f,100f,null);
		GUILayout.EndHorizontal();

		return sliderPosition;
	}

	void OnSceneGUI(){
		int controlId = GUIUtility.GetControlID(FocusType.Passive);
		Event e = Event.current;
		Ray ray = Camera.current.ScreenPointToRay (new Vector3(e.mousePosition.x, -e.mousePosition.y + Camera.current.pixelHeight));
		Vector3 mousePos = ray.origin;

		// create tile
		if(e.isMouse && e.type ==EventType.MouseDown && e.button == 0){
			GUIUtility.hotControl = controlId;
			e.Use();

			if(grid.draggable){
				mouseBeginPos = mousePos;
			}else{

				GameObject gameObject;
				Transform prefab = grid.tilePrefab;

				if(prefab){
					Undo.IncrementCurrentGroup();
					Vector3 aligned = new Vector3(Mathf.Floor (mousePos.x/grid.width) * grid.width + grid.width/2.0f,Mathf.Floor (mousePos.y/grid.height) * grid.height + grid.height/2.0f,0.0f);	

					if(GetTransformFromPosition(aligned) != null) return;

					gameObject = (GameObject) PrefabUtility.InstantiatePrefab (prefab.gameObject);
					gameObject.transform.position = aligned;
					gameObject.transform.parent = grid.transform;

					Undo.RegisterCreatedObjectUndo(gameObject, " Create " + gameObject.name);
				}
			}
		}

		if(e.isMouse & e.type == EventType.MouseDown && (e.button == 0 || e.button == 1)){
			GUIUtility.hotControl = controlId;
			e.Use();
			Vector3 aligned = new Vector3(Mathf.Floor (mousePos.x/grid.width) * grid.width + grid.width/2.0f,Mathf.Floor (mousePos.y/grid.height) * grid.height + grid.height/2.0f,0.0f);
			Transform transform = GetTransformFromPosition(aligned);
			if(transform != null){
				DestroyImmediate( transform.gameObject);
			}
		}

		if((e.isMouse && e.type == EventType.MouseUp && (e.button == 0 || e.button == 1))){
			if(grid.draggable && e.button == 0){
				mouseEndPos = mousePos;
				FillArea(mouseBeginPos, mouseEndPos);

				mouseEndPos 		= Vector3.zero;
				mouseBeginPos 	= Vector3.zero;
			}

			GUIUtility.hotControl = 0;
		}

	
	}


	Transform GetTransformFromPosition( Vector3 aligned){

		int i = 0;
		while( i < grid.transform.childCount){
			Transform transform = grid.transform.GetChild (i);
			if( transform.position == aligned){
				return transform;
			}

			i++;
		}
		return null;
	}


	void FillArea(Vector3 _StartPosition, Vector3 _EndPosition){

		Transform prefab = grid.tilePrefab;
		if(prefab == null){
			Debug.LogError("No prefab attached to grid.");
		}

		// contains the number of tiles to the real start position, this means we begin in (0,0,0).... our value might be (2.0f,1.0f) then we begin at the position = 0 + 2.0f * grid.width
		Vector2 numberOfTilesToStartPosition = new Vector2();
		Vector2 numberOfTilesToEndPosition = new Vector2();

		Vector2 tilesToFill = new Vector2();
		_StartPosition.x = Mathf.Floor(_StartPosition.x /grid.width) * grid.width;
		_StartPosition.y = Mathf.Floor(_StartPosition.y /grid.height) * grid.height;

		_EndPosition.x = Mathf.Floor(_EndPosition.x /grid.width) * grid.width;
		_EndPosition.y = Mathf.Floor(_EndPosition.y /grid.height) * grid.height ;

		Vector2 numberOfTilesToFill = new Vector2();


		// look if there is a drag from right to left or bottom to top
		// if so swap entries
		if(_StartPosition.x > _EndPosition.x){
			numberOfTilesToFill.x = _StartPosition.x - _EndPosition.x;
		}else{
			numberOfTilesToFill.x = _EndPosition.x - _StartPosition.x;
		}

		if(_StartPosition.y > _EndPosition.y){
			numberOfTilesToFill.y = _StartPosition.y - _EndPosition.y;
		}else{
			numberOfTilesToFill.y = _EndPosition.y - _StartPosition.y;
		}

		// swap to fill from left to right
		if(_StartPosition.x >= _EndPosition.x){
			Vector3 tmp = new Vector3();
			tmp = _EndPosition;
			_StartPosition   	= _EndPosition;
			_EndPosition	 	= tmp;
		}

		numberOfTilesToFill.x = numberOfTilesToFill.x / grid.width + 1.0f;
		numberOfTilesToFill.y = numberOfTilesToFill.y / grid.height + 1.0f;
		// save them 
		int currentXTileNumber = 0;
		int currentYTileNumber = 0;


		do{
			currentYTileNumber = 0;
			do{

				Vector3 realWorldPosition = new Vector3();
				GameObject gameObject;
				
				realWorldPosition.x = _StartPosition.x + (currentXTileNumber * grid.width)  + grid.width / 2.0f;
				realWorldPosition.y = _StartPosition.y - (currentYTileNumber * grid.height) + grid.height / 2.0f;
				realWorldPosition.z = 0.0f;
			//	if(GetTransformFromPosition(realWorldPosition) != null) continue;
				
				gameObject = (GameObject) PrefabUtility.InstantiatePrefab (prefab.gameObject);
				gameObject.transform.position	= realWorldPosition;
				gameObject.transform.parent 	= grid.transform;


				++currentYTileNumber;
			}while(currentYTileNumber < numberOfTilesToFill.y);

			++ currentXTileNumber;
		}while(currentXTileNumber < numberOfTilesToFill.x);

	}

	//	void FillArea(Vector3 beginPos, Vector3 endPos){
//		int negX = 1;
//		int negY = 1;
//		int modX = 0;
//		int modY = 0;
//
//		Vector3 beginChunks = new Vector3(Mathf.Floor (beginPos.x/grid.width) ,Mathf.Floor (beginPos.y/grid.height) ,0.0f);
//		Vector3 endChunks =  new Vector3(Mathf.Floor (endPos.x/grid.width) ,Mathf.Floor (endPos.y/grid.height) ,0.0f);
//
//		Vector3 beginChunk = new Vector3(Mathf.Floor (beginPos.x/grid.width),Mathf.Floor (beginPos.y/grid.height) ,0.0f);
//
//		Vector3 tilesToDraw = endChunks - beginChunks;
//
//		int x = (int)tilesToDraw.x; // +1 because of first tile is 0
//		int y = (int)tilesToDraw.y;
//
//		if(x < 0) {
//			negX = -1;
//			x = (-1)*(x-1);
//			modX = -1;
//		}else{
//			x = x+1;
//		}
//		if(y < 0) 
//		{
//			negY = -1;
//			y = (-1)*(y-1);
//			modY = -1;
//		}else{
//			y = y + 1;
//		}
//
//		Debug.Log ("x " + x + " " + y);
//		Transform prefab = grid.tilePrefab;
//	//	Undo.IncrementCurrentGroup();
//		Vector3 aligned = new Vector3();
//
//
//		for(int i = 0; i< x; i++){
//
//			Debug.Log (i);
//			aligned.x = (i+modX)*grid.width*negX+(beginChunk.x*grid.width) +grid.width*negX/2;
//			for(int j =0; j<y; j++){
//				GameObject gameObject;
//
//				if(prefab){
//
//					aligned.y =( j+modY)*grid.height*negY+(beginChunk.y*grid.height)+grid.height*negY/2;
//					aligned.z = 0 ;
//					Debug.Log(aligned.ToString());
//					if(GetTransformFromPosition(aligned) != null) continue;
//					
//					gameObject = (GameObject) PrefabUtility.InstantiatePrefab (prefab.gameObject);
//					gameObject.transform.position = aligned;
//					gameObject.transform.parent = grid.transform;
//
//				}
//
//			}
//		}
//		//Undo.RegisterCreatedObjectUndo(gameObject, " Create Gameobjects " + gameObject.name);
//
//	}
}
