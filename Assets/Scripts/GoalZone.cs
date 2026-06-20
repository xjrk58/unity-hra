using UnityEngine;

/// <summary>
/// Trigger volume marking the goal ("cíl"). Flags competitors as being inside,
/// which lets them expel their backpack here.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GoalZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var c = other.GetComponent<Competitor>();
        if (c != null) c.InGoal = true;
    }

    void OnTriggerExit(Collider other)
    {
        var c = other.GetComponent<Competitor>();
        if (c != null) c.InGoal = false;
    }
}
