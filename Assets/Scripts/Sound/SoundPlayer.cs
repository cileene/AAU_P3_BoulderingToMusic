using Configs;
using UnityEngine;
using FMODUnity;

namespace Sound
{
    public class SoundPlayer : MonoBehaviour
    {
        private GameObject _handholdSound;
        private GameObject _bgmSound;
        private GameObject _winSound;
        private GameObject _fallSound;
        

        private void OnEnable()
        {
            Debug.Log("[HandholdSoundPlayer] OnEnable – subscribing to events");
            AppEvents.SoundConfig += OnSoundConfig;
            AppEvents.PotentialHandholdContact += PlayHandholdSound;
            AppEvents.PotentialHighestHandholdContact += PlayWinSound;
            AppEvents.PotentialFallDetected += PlayFallSound;
        }

        private void OnDisable()
        {
            Debug.Log("[HandholdSoundPlayer] OnDisable – unsubscribing from events");
            AppEvents.SoundConfig -= OnSoundConfig;
            AppEvents.PotentialHandholdContact -= PlayHandholdSound;
            AppEvents.PotentialHighestHandholdContact -= PlayWinSound;
            AppEvents.PotentialFallDetected -= PlayFallSound;
        }

        private void OnSoundConfig(SoundConfig config)
        {
            _handholdSound = config.HandholdsSound;
            _bgmSound = config.BgmSound;
            _winSound = config.WinSound;
            _fallSound = config.FallSound;
        }

        private void TriggerEmitter(GameObject soundObject)
        {
            if (!soundObject) return;

            var emitter = soundObject.GetComponent<StudioEventEmitter>();
            if (!emitter) return;
            
            emitter.Play();
        }

        private void PlayHandholdSound() => TriggerEmitter(_handholdSound);

        private void PlayWinSound() => TriggerEmitter(_winSound);

        private void PlayFallSound() => TriggerEmitter(_fallSound);
    }
}
