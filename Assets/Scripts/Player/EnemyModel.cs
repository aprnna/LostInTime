namespace Player
{
    public class EnemyModel
    {
        public string EnemyName;
        public int Health;
        public int MaxHealth;
        public int BaseDamage;
        public int IntervalDamage;

        public EnemyModel(EnemySO enemyData)
        {
            EnemyName = enemyData.EnemyName;
            Health = enemyData.Health;
            MaxHealth = enemyData.MaxHealth;
            BaseDamage = enemyData.BaseDamage;
            IntervalDamage = enemyData.IntervalDamage;
        }
    }
}