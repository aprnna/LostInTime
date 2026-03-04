using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Minigames
{
    public abstract class MinigameComponent: MonoBehaviour
    {
        protected bool _initialized = false;

        protected abstract UniTask OnInit();
        
        public async UniTask Init()
        {
            await OnInit();
            _initialized = true;
        }

        public virtual void OnReset()
        {

        }
    }
}