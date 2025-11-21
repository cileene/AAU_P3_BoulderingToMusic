using UnityEngine;

namespace Sound
{
    public class HandholdSoundPlayer : MonoBehaviour
    {
        private GameObject _handholdSound;
        private GameObject _bgmSound;
        private GameObject _winSound;
        private GameObject _deathSound;

        private void OnEnable()
        {
            Debug.Log("[HandholdSoundPlayer] OnEnable – subscribing to events");
            AppEvents.SoundConfig += OnSoundConfig;
            AppEvents.PotentialHandholdContact += PlayHandholdSound;
            AppEvents.PotentialHighestHandholdContact += PlayWinSound;
            AppEvents.PotentialFallDetected += PlayDeathSound;
        }

        private void OnDisable()
        {
            Debug.Log("[HandholdSoundPlayer] OnDisable – unsubscribing from events");
            AppEvents.SoundConfig -= OnSoundConfig;
            AppEvents.PotentialHandholdContact -= PlayHandholdSound;
            AppEvents.PotentialHighestHandholdContact -= PlayWinSound;
            AppEvents.PotentialFallDetected -= PlayDeathSound;
        }

        private void OnSoundConfig(GameObject handholdsSound, GameObject bgmSound, GameObject winSound,
            GameObject deathSound)
        {
            _handholdSound = handholdsSound;
            _bgmSound = bgmSound;
            _winSound = winSound;
            _deathSound = deathSound;
            
            _bgmSound.GetComponent<FMODUnity.StudioEventEmitter>()?.Play();
        }

        private void TriggerEmitter(GameObject soundObject)
        {
            if (!soundObject) return;

            var emitter = soundObject.GetComponent<FMODUnity.StudioEventEmitter>();
            if (!emitter) return;
            
            emitter.Play();
        }

        private void PlayHandholdSound()
        {
            TriggerEmitter(_handholdSound);
            Debug.Log("Played handhold contact sound");
        }

        private void PlayWinSound()
        {
            TriggerEmitter(_winSound);
            Debug.Log("Played win sound");
        }

        private void PlayDeathSound()
        {
            TriggerEmitter(_deathSound);
            Debug.Log("Played death sound");
        }
    }
}
