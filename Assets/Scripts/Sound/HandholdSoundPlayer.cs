using System.Collections;
using UnityEngine;

namespace Sound
{
    public class HandholdSoundPlayer : MonoBehaviour
    {
        private GameObject handholdSound;
        private GameObject bgmSound;
        private GameObject winSound;
        private GameObject deathSound;

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
            this.handholdSound = handholdsSound;
            this.bgmSound = bgmSound;
            this.winSound = winSound;
            this.deathSound = deathSound;
        }

        private void TriggerEmitter(GameObject soundObject)
        {
            if (soundObject == null) return;

            var emitter = soundObject.GetComponent<FMODUnity.StudioEventEmitter>();
            if (emitter == null) return;

            // Reset
            emitter.enabled = false;

            // Trigger FMOD "Object Enable"
            emitter.enabled = true;

            // Disable again next frame for clean oneshot behavior
            StartCoroutine(DisableEmitterNextFrame(emitter));
        }

        private IEnumerator DisableEmitterNextFrame(FMODUnity.StudioEventEmitter emitter)
        {
            yield return null; // wait one frame
            emitter.enabled = false;
        }

        private void PlayHandholdSound()
        {
            TriggerEmitter(this.handholdSound);
        }

        private void PlayWinSound()
        {
            TriggerEmitter(this.winSound);
        }

        private void PlayDeathSound()
        {
            TriggerEmitter(this.deathSound);
        }
    }
}
