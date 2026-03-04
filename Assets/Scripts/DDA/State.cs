namespace DDA
{
    public enum HPState { Low, Medium, High }
    public enum TimeState { Slow, Normal, Fast }
    public enum DamageState { High, Medium, Low }

    public struct State
    {
        public HPState hp;
        public TimeState time;
        public DamageState damage;

        public State(HPState hp, TimeState time, DamageState damage)
        {
            this.hp = hp;
            this.time = time;
            this.damage = damage;
        }
        public override bool Equals(object obj)
        {
            if (!(obj is State)) return false;
            State other = (State)obj;
            return hp == other.hp && time == other.time && damage == other.damage;
        }

        public override int GetHashCode()
        {
            return ((int)hp * 100) + ((int)time * 10) + (int)damage;
        }
    }
}