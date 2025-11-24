using UnityEngine;

[CreateAssetMenu(menuName = "GameData", fileName = "GameData")]

public class GameData: LostInTimeData
{
    [field:SerializeField] public bool PrologCompleted { get; set; }

}
