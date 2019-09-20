using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 跟随
/// </summary>
public class FollowObject : MonoBehaviour
{
	public Transform target;
	public Vector3 offset;

	private void LateUpdate()
	{
		transform.position = target.position + offset;
	}
}
