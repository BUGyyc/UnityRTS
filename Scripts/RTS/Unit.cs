using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;
using UnityEngine.Events;
/// <summary>
/// 士兵单位的行为
/// </summary>
public class Unit : MonoBehaviour
{
	/// <summary>
	/// 士兵所处状态
	/// </summary>
	public UnitState state = UnitState.Idle;
	public UnitTemplate template;
	/// <summary>
	/// 导航
	/// </summary>
	private NavMeshAgent navMeshAgent;
	/// <summary>
	/// 动画
	/// </summary>
	private Animator animator;
	/// <summary>
	/// 光圈
	/// </summary>
	private SpriteRenderer selectionCircle;

	//private bool isSelected; //is the Unit currently selected by the Player
	/// <summary>
	/// 攻击对象
	/// </summary>
	private Unit targetOfAttack;
	private Unit[] hostiles;
	private float lastGuardCheckTime, guardCheckInterval = 1f;
	private bool isReady = false;

	public UnityAction<Unit> OnDie;

	void Awake ()
	{
		navMeshAgent = GetComponent<NavMeshAgent>();
		animator = GetComponent<Animator>();
		selectionCircle = transform.Find("SelectionCircle").GetComponent<SpriteRenderer>();

		//随机一个速度
		float rndmFactor = navMeshAgent.speed * .15f;
		navMeshAgent.speed += Random.Range(-rndmFactor, rndmFactor);
	}

	private void Start()
	{
		template = Instantiate<UnitTemplate>(template); //we copy the template otherwise it's going to overwrite the original asset!

		//Set some defaults, including the default state
		SetSelected(false);
		Guard();
	}
	
	void Update()
	{
		//Little hack to give time to the NavMesh agent to set its destination.
		//without this, the Unit would switch its state before the NavMeshAgent can kick off, leading to unpredictable results
		if(!isReady)
		{
			isReady = true;
			return;
		}
		//不同状态执行不同行为
		switch(state)
		{
			case UnitState.MovingToSpotIdle:
				if(navMeshAgent.remainingDistance < navMeshAgent.stoppingDistance + .1f)
				{
					Stop();
				}
				break;

			case UnitState.MovingToSpotGuard:
				if(navMeshAgent.remainingDistance < navMeshAgent.stoppingDistance + .1f)
				{
					Guard();
				}
				break;

			case UnitState.MovingToTarget:
				//check if target has been killed by somebody else
				if(IsDeadOrNull(targetOfAttack))
				{
					Guard();
				}
				else
				{
					//Check for distance from target
					if(navMeshAgent.remainingDistance < template.engageDistance)
					{
						navMeshAgent.velocity = Vector3.zero;
						StartAttacking();
					}
					else
					{
						navMeshAgent.SetDestination(targetOfAttack.transform.position); //update target position in case it's moving
					}
				}

				break;

			case UnitState.Guarding:
				if(Time.time > lastGuardCheckTime + guardCheckInterval)
				{
					lastGuardCheckTime = Time.time;
					Unit t = GetNearestHostileUnit();
					if(t != null)
					{
						MoveToAttack(t);
					}
				}
				break;
			case UnitState.Attacking:
				//check if target has been killed by somebody else
				if(IsDeadOrNull(targetOfAttack))
				{
					Guard();
				}
				else
				{
					//look towards the target
					Vector3 desiredForward = (targetOfAttack.transform.position - transform.position).normalized;
					transform.forward = Vector3.Lerp(transform.forward, desiredForward, Time.deltaTime * 10f);
				}
				break;
		}

		float navMeshAgentSpeed = navMeshAgent.velocity.magnitude;
		animator.SetFloat("Speed", navMeshAgentSpeed * .05f);
	}

	/// <summary>
	/// 执行命令
	/// </summary>
	/// <param name="c"></param>
	public void ExecuteCommand(AICommand c)
	{
		if(state == UnitState.Dead)
		{
			//已经死亡，不执行命令
			return;
		}

		switch(c.commandType)
		{
			case AICommand.CommandType.GoToAndIdle:
				GoToAndIdle(c.destination);
				break;

			case AICommand.CommandType.GoToAndGuard:
				GoToAndGuard(c.destination);
				break;

			case AICommand.CommandType.Stop:
				Stop();
				break;

			case AICommand.CommandType.AttackTarget:
				MoveToAttack(c.target);
				break;
			
			case AICommand.CommandType.Die:
				Die();
				break;
		}
	}
		
	/// <summary>
	/// 移动到目标位置，默认状态
	/// </summary>
	/// <param name="location"></param>
	private void GoToAndIdle(Vector3 location)
	{
		state = UnitState.MovingToSpotIdle;
		targetOfAttack = null;
		isReady = false;

		navMeshAgent.isStopped = false;
		navMeshAgent.SetDestination(location);
	}

	/// <summary>
	/// 移动到目标位置，戒备状态
	/// </summary>
	/// <param name="location"></param>
	private void GoToAndGuard(Vector3 location)
	{
		state = UnitState.MovingToSpotGuard;
		targetOfAttack = null;
		isReady = false;

		navMeshAgent.isStopped = false;
		navMeshAgent.SetDestination(location);
	}

	/// <summary>
	/// 停止
	/// </summary>
	private void Stop()
	{
		state = UnitState.Idle;
		targetOfAttack = null;
		isReady = false;

		navMeshAgent.isStopped = true;
		navMeshAgent.velocity = Vector3.zero;
	}

	/// <summary>
	/// 戒备状态，并且站在原地
	/// </summary>
	public void Guard()
	{
		state = UnitState.Guarding;
		targetOfAttack = null;
		isReady = false;

		navMeshAgent.isStopped = true;
		navMeshAgent.velocity = Vector3.zero;
	}

	/// <summary>
	/// 朝目标移动过去，并且到达后攻击
	/// </summary>
	/// <param name="target"></param>
	private void MoveToAttack(Unit target)
	{
		if(!IsDeadOrNull(target))
		{
			state = UnitState.MovingToTarget;
			targetOfAttack = target;
			isReady = false;

			navMeshAgent.isStopped = false;
			navMeshAgent.SetDestination(target.transform.position);
		}
		else
		{
			//if the command is dealt by a Timeline, the target might be already dead
			Guard();
		}
	}

	/// <summary>
	/// 开始攻击
	/// </summary>
	private void StartAttacking()
	{
		//somebody might have killed the target while this Unit was approaching it
		if(!IsDeadOrNull(targetOfAttack))
		{
			state = UnitState.Attacking;
			isReady = false;
			navMeshAgent.isStopped = true;
			StartCoroutine(DealAttack());
		}
		else
		{
			Guard();
		}
	}

	/// <summary>
	/// 执行攻击
	/// </summary>
	/// <returns></returns>
	private IEnumerator DealAttack()
	{
		while(targetOfAttack != null)
		{
			animator.SetTrigger("DoAttack");
			targetOfAttack.SufferAttack(template.attackPower);

			yield return new WaitForSeconds(1f / template.attackSpeed);

			//check is performed after the wait, because somebody might have killed the target in the meantime
			if(IsDeadOrNull(targetOfAttack))
			{
				animator.SetTrigger("InterruptAttack");
				break;

			}

			if(state == UnitState.Dead)
			{
				yield break;
			}

			//Check if the target moved away for some reason
			if(Vector3.Distance(targetOfAttack.transform.position, transform.position) > template.engageDistance)
			{
				MoveToAttack(targetOfAttack);
			}
		}


		//only move into Guard if the attack was interrupted (dead target, etc.)
		if(state == UnitState.Attacking)
		{
			Guard();
		}
	}

	/// <summary>
	/// 被攻击
	/// </summary>
	/// <param name="damage"></param>
	private void SufferAttack(int damage)
	{
		if(state == UnitState.Dead)
		{
			//already dead
			return;
		}

		template.health -= damage;

		if(template.health <= 0)
		{
			template.health = 0;
			Die();
		}
	}

	/// <summary>
	/// 死亡
	/// </summary>
	private void Die()
	{
		state = UnitState.Dead; //still makes sense to set it, because somebody might be interacting with this script before it is destroyed
		animator.SetTrigger("DoDeath");

		//Remove itself from the selection Platoon
		GameManager.Instance.RemoveFromSelection(this);
		SetSelected(false);
		
		//Fire an event so any Platoon containing this Unit will be notified
		if(OnDie != null)
		{
			OnDie(this);
		}

		//To avoid the object participating in any Raycast or tag search
		gameObject.tag = "Untagged";
		gameObject.layer = 0;

		//Remove unneeded Components
		Destroy(selectionCircle);
		Destroy(navMeshAgent);
		Destroy(GetComponent<Collider>()); //will make it unselectable on click
		Destroy(animator, 4f); //give it some time to complete the animation
		Destroy(this);
	}

	/// <summary>
	/// 判断是否是空或者死亡了
	/// </summary>
	/// <param name="u"></param>
	/// <returns></returns>
	private bool IsDeadOrNull(Unit u)
	{
		return (u == null || u.state == UnitState.Dead);
	}

	private Unit GetNearestHostileUnit()
	{
		hostiles = GameObject.FindGameObjectsWithTag(template.GetOtherFaction().ToString()).Select(x => x.GetComponent<Unit>()).ToArray();

		Unit nearestEnemy = null;
		float nearestEnemyDistance = 1000f;
		for(int i=0; i<hostiles.Count(); i++)
		{
			if(IsDeadOrNull(hostiles[i]))
			{
				continue;
			}

			float distanceFromHostile = Vector3.Distance(hostiles[i].transform.position, transform.position);
			if(distanceFromHostile <= template.guardDistance)
			{
				if(distanceFromHostile < nearestEnemyDistance)
				{
					nearestEnemy = hostiles[i];
					nearestEnemyDistance = distanceFromHostile;
				}
			}
		}

		return nearestEnemy;
	}

	/// <summary>
	/// 设置选中状态
	/// </summary>
	/// <param name="selected"></param>
	public void SetSelected(bool selected)
	{
		Color newColor = selectionCircle.color;
		newColor.a = (selected) ? 1f : .3f;
		selectionCircle.color = newColor;
	}

	/// <summary>
	/// 士兵的状态
	/// </summary>
	public enum UnitState
	{
		Idle,//默认状态
		Guarding,//戒备状态
		Attacking,//攻击状态
		MovingToTarget,//朝目标移动
		MovingToSpotIdle,//朝地点移动
		MovingToSpotGuard,//移动到戒备
		Dead,//死亡
	}

	private void OnDrawGizmos()
	{
		if(navMeshAgent != null
			&& navMeshAgent.isOnNavMesh
			&& navMeshAgent.hasPath)
		{
			Gizmos.DrawLine(transform.position, navMeshAgent.destination);
		}
	}
}