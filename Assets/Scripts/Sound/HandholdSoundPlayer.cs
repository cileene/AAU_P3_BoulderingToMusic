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
            AppEvents.SoundConfig += OnSoundConfig;
            AppEvents.PotentialHandholdContact += PlayHandholdSound;
        }

        private void OnDisable()
        {
            AppEvents.SoundConfig -= OnSoundConfig;
            AppEvents.PotentialHandholdContact -= PlayHandholdSound;
        }

        private void OnSoundConfig(GameObject handholdsSound, GameObject bgmSound, GameObject winSound, GameObject deathSound)
        {
            this.handholdSound = handholdSound;
            this.bgmSound = bgmSound;
            this.winSound = winSound;
            this.deathSound = deathSound;
        }

        private void Start()
        {
            if (bgmSound == null) return;
            bgmSound.SetActive(true);
        }

        private void PlayHandholdSound()
        {
            if (handholdSound == null) return;

            handholdSound.SetActive(true);
            handholdSound.SetActive(false);
        }

        private void PlayWinSound()
        {
            if (winSound == null) return;
            winSound.SetActive(true);
            
        }
    }
}
