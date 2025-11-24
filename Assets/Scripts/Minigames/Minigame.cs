using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Minigames
{
    public abstract class Minigame : MonoBehaviour
    {
        public enum Result
        {
            Success, Failure, Cancelled
        }

        protected abstract MinigameComponent[] MinigameComponents();
        protected bool isPlaying = false;
        private Result _result;
        protected UniTaskCompletionSource<Result> completionSource;
        public async UniTask Init()
        {
            var components = MinigameComponents();
            if(components != null)
            {
                foreach (var component in components)
                {
                    await component.Init();
                }
            }
            gameObject.SetActive(false);
            await OnInit();
        }
        public abstract void OnPlay(float difficulty);
        protected abstract UniTask OnInit();
        protected virtual void OnCancel() { }
        protected virtual void OnPrepare(float difficulty)
        {
        }
        private UniTask Prepare(float difficulty)
        {
            OnPrepare(difficulty);
            return UniTask.CompletedTask;
        }
        protected void SetResult(Result result)
        {
            isPlaying = false;
            _result = result;
            completionSource.TrySetResult(_result);
            gameObject.SetActive(false);
            OnCleanUp();
        }
        public void Cancel()
        {
            SetResult(Result.Cancelled);
            OnCancel();
        }
        private float _difficulty;
        protected float Difficulty => _difficulty;
        protected abstract void OnCleanUp();
        protected virtual void Update()
        {
            if (isPlaying)
            {
                OnPlay(_difficulty);
            }
        }
        public async UniTask<Result> Play(float difficulty)
        {
            completionSource = new UniTaskCompletionSource<Result>();
            gameObject.SetActive(true);
            isPlaying = true;
            _difficulty = difficulty;
            await Prepare(difficulty);
            return await completionSource.Task;
        }
        private void OnDestroy()
        {
            SetResult(Result.Cancelled);
        }
    }
}