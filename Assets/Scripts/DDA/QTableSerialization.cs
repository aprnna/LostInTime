using System.Collections.Generic;
namespace DDA
{
    [System.Serializable]
    public class QTableEntry
    {
        public HPState hp;
        public TimeState time;
        public DamageState damage;
        public float[] qValues;
    }

    [System.Serializable]
    public class QTableData
    {
        public List<QTableEntry> entries = new List<QTableEntry>();
    }
}