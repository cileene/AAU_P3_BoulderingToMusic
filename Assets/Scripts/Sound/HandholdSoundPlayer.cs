using System.Collections;
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
        }

        private void TriggerEmitter(GameObject soundObject)
        {
            if (soundObject == null) return;

            var emitter = soundObject.GetComponent<FMODUnity.StudioEventEmitter>();
            if (emitter == null) return;

            // Reset
            emitter.Play();
        }

        private IEnumerator DisableEmitterNextFrame(FMODUnity.StudioEventEmitter emitter)
        {
            yield return null; // wait one frame
            emitter.enabled = false;
        }

        private void PlayHandholdSound()
        {
            TriggerEmitter(_handholdSound);
        }

        private void PlayWinSound()
        {
            TriggerEmitter(_winSound);
        }

        private void PlayDeathSound()
        {
            TriggerEmitter(_deathSound);
        }
    }
}
