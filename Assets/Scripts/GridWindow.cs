using UnityEngine;
using UnityEditor;
using System.Collections;

public class GridWindow : EditorWindow {
	Grid grid;

	public void init(){
		grid = (Grid)FindObjectOfType(typeof(Grid));
	}

	void OnGUI(){
		grid.color = EditorGUILayout.ColorField(grid.color,GUILayout.Width(200));
	}
}
