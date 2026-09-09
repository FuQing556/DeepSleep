using System.Threading;

namespace DeepSleep.Runtime.Combat.Damage
{
    /// <summary>为一次攻击分配本局唯一身份；池对象每次重新发射都必须重新领取。</summary>
    public static class DamageAttackIdAllocator
    {
        private static long _lastId;

        public static ulong Next()
        {
            ulong id = unchecked((ulong)Interlocked.Increment(ref _lastId));
            return id != 0
                ? id
                : unchecked((ulong)Interlocked.Increment(ref _lastId));
        }
    }
}
