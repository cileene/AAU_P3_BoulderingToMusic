using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisionModels.Utilities
{
    public static class AnnotationManager
    {
        public static GameObject GetOrCreateBox(
            List<GameObject> pool,
            int index,
            Transform parent,
            Sprite borderSprite,
            Font font,
            Color color)
        {
            if (index < pool.Count)
            {
                pool[index].SetActive(true);
                return pool[index];
            }

            return CreateNewBox(pool, parent, borderSprite, font, color);
        }

        public static void ClearAnnotations(List<GameObject> pool)
        {
            foreach (var obj in pool)
                obj.SetActive(false);
        }

        private static GameObject CreateNewBox(
            List<GameObject> pool,
            Transform parent,
            Sprite borderSprite,
            Font font,
            Color color)
        {
            var panel = new GameObject("ObjectBox");
            panel.AddComponent<CanvasRenderer>();
            var img = panel.AddComponent<Image>();
            img.color = color;
            img.sprite = borderSprite;
            img.type = Image.Type.Sliced;
            panel.transform.SetParent(parent, false);

            var text = new GameObject("ObjectLabel");
            text.AddComponent<CanvasRenderer>();
            text.transform.SetParent(panel.transform, false);
            var txt = text.AddComponent<Text>();
            txt.font = font;
            txt.color = color;
            txt.fontSize = 40;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;

            var rt2 = text.GetComponent<RectTransform>();
            rt2.offsetMin = new Vector2(20, 0);
            rt2.offsetMax = new Vector2(0, 30);
            rt2.anchorMin = new Vector2(0, 0);
            rt2.anchorMax = new Vector2(1, 1);

            pool.Add(panel);
            return panel;
        }
    }
}