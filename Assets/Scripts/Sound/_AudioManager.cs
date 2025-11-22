using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace Sound
{
    public class _AudioManager : MonoBehaviour
    {
        public static _AudioManager Instance { get; private set; }

        private EventInstance musicEventInstance;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.Log("Found more than one audio manager in the scene");
            }
            Instance = this;
        }

        private void Start()
        {
            InitializeMusic(_FMODEvents.Instance.Ambience);
        }

        private void InitializeMusic(EventReference musicEventReference)
        {
            musicEventInstance = CreateEventInstance(musicEventReference);
            musicEventInstance.start();
        }

        public EventInstance CreateEventInstance(EventReference eventReference)
        {
            EventInstance eventInstance = RuntimeManager.CreateInstance(eventReference);
            return eventInstance;
        }

        public void PlayOneShot(EventReference sound, Vector3 worldPos)
        {
            RuntimeManager.PlayOneShot(sound, worldPos);
        }
    }
}