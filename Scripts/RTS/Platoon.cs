using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 军队
/// </summary>
[Serializable]
public class Platoon : MonoBehaviour {
	public List<Unit> units;

	private Vector3[] tempPositions; //an array to do position calculations, doesn't necessary represent the position of the units at the moment
	private float formationOffset = 3f;

	private void Start () {
		for (int i = 0; i < units.Count; i++) {
			units[i].OnDie += UnitDeadHandler;
		}
	}

	/// <summary>
	/// 执行命令
	/// </summary>
	/// <param name="command"></param>
	public void ExecuteCommand (AICommand command) {
		tempPositions = GetFormationPositions (command.destination);
		for (int i = 0; i < units.Count; i++) {
			if (units.Count > 1) {
				//change the position for the command for each unit
				//so they move to a formation position rather than in the exact same place
				command.destination = tempPositions[i];
			}

			units[i].ExecuteCommand (command);
		}
	}

	/// <summary>
	/// 加入士兵
	/// </summary>
	/// <param name="unitToAdd"></param>
	public void AddUnit (Unit unitToAdd) {
		unitToAdd.OnDie += UnitDeadHandler;
		units.Add (unitToAdd);
	}

	/// <summary>
	/// 加入多个士兵
	/// </summary>
	/// <param name="unitsToAdd"></param>
	/// <returns></returns>
	public int AddUnits (Unit[] unitsToAdd) {
		for (int i = 0; i < unitsToAdd.Length; i++) {
			AddUnit (unitsToAdd[i]);
		}
		return units.Count;
	}

	/// <summary>
	/// 移除士兵
	/// </summary>
	/// <param name="unitToRemove"></param>
	/// <returns></returns>
	public bool RemoveUnit (Unit unitToRemove) {
		bool isThere = units.Contains (unitToRemove);

		if (isThere) {
			units.Remove (unitToRemove);
			unitToRemove.OnDie -= UnitDeadHandler;
		}

		return isThere;
	}

	/// <summary>
	/// 清空士兵组
	/// </summary>
	public void Clear () {
		for (int i = 0; i < units.Count; i++) {
			units[i].OnDie -= UnitDeadHandler;
		}
		units.Clear ();
	}

	/// <summary>
	/// 所有士兵的坐标
	/// </summary>
	/// <returns></returns>
	public Vector3[] GetCurrentPositions () {
		tempPositions = new Vector3[units.Count];
		for (int i = 0; i < units.Count; i++) {
			tempPositions[i] = units[i].transform.position;
		}
		return tempPositions;
	}

	/// <summary>
	/// 获得队形坐标
	/// </summary>
	/// <param name="formationCenter"></param>
	/// <returns></returns>
	public Vector3[] GetFormationPositions (Vector3 formationCenter) {
		tempPositions = new Vector3[units.Count];
		float increment = 360f / units.Count;
		for (int k = 0; k < units.Count; k++) {
			float angle = increment * k;
			Vector3 offset = new Vector3 (formationOffset * Mathf.Cos (angle * Mathf.Deg2Rad), 0f, formationOffset * Mathf.Sin (angle * Mathf.Deg2Rad));
			tempPositions[k] = formationCenter + offset;
		}
		return tempPositions;
	}

	/// <summary>
	/// 设置坐标
	/// </summary>
	/// <param name="_newPositions"></param>
	public void SetPositions (Vector3[] _newPositions) {
		for (int i = 0; i < units.Count; i++) {
			units[i].transform.position = _newPositions[i];
		}
	}

	/// <summary>
	/// 检测是否全部死亡
	/// </summary>
	/// <returns></returns>
	public bool CheckIfAllDead () {
		bool allDead = true;
		for (int i = 0; i < units.Count; i++) {
			if (units[i] != null &&
				units[i].state != Unit.UnitState.Dead) {
				allDead = false;
				break;
			}
		}
		return allDead;
	}

	/// <summary>
	/// 死亡状态调用的方法
	/// </summary>
	/// <param name="whoDied"></param>
	private void UnitDeadHandler (Unit whoDied) {
		RemoveUnit (whoDied); //will also remove the handler
	}

	private void OnDrawGizmosSelected () {
		for (int i = 0; i < units.Count; i++) {
			if (units[i] != null) {
				Gizmos.color = new Color (.8f, .8f, 1f, 1f);
				Gizmos.DrawCube (units[i].transform.position, new Vector3 (1f, .1f, 1f));
			}
		}
	}
}