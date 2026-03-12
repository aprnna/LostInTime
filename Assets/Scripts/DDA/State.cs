using System;

namespace DDA
{
    public enum HPState { Low, Medium, High }
    public enum TimeState { Slow, Normal, Fast }
    public enum DamageState { High, Medium, Low }

    public struct State : IEquatable<State>
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

        public bool Equals(State other)
        {
            return hp == other.hp && time == other.time && damage == other.damage;
        }

        public override bool Equals(object obj)
        {
            return obj is State other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(hp, time, damage);
        }

        public override string ToString()
        {
            return $"HP:{hp}_Time:{time}_Dmg:{damage}";
        }
    }
}