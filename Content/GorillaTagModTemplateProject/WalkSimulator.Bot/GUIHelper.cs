<<<<<<< HEAD
using UnityEngine;

namespace WalkSimulator.Bot
{
    public static class GUIHelper
    {
        public static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            result.filterMode = FilterMode.Bilinear;
            result.wrapMode = TextureWrapMode.Clamp;
            return result;
        }

        public static Texture2D MakeRoundedRectTexture(int width, int height, Color col, int roundPixels)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            Color clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = true;
                    if (x < roundPixels && y < roundPixels)
                    {
                        float dx = roundPixels - x;
                        float dy = roundPixels - y;
                        inside = (dx * dx + dy * dy) <= (roundPixels * roundPixels);
                    }
                    else if (x >= width - roundPixels && y < roundPixels)
                    {
                        float dx = x - (width - roundPixels);
                        float dy = roundPixels - y;
                        inside = (dx * dx + dy * dy) <= (roundPixels * roundPixels);
                    }
                    else if (x < roundPixels && y >= height - roundPixels)
                    {
                        float dx = roundPixels - x;
                        float dy = y - (height - roundPixels);
                        inside = (dx * dx + dy * dy) <= (roundPixels * roundPixels);
                    }
                    else if (x >= width - roundPixels && y >= height - roundPixels)
                    {
                        float dx = x - (width - roundPixels);
                        float dy = y - (height - roundPixels);
                        inside = (dx * dx + dy * dy) <= (roundPixels * roundPixels);
                    }
                    tex.SetPixel(x, y, inside ? col : clear);
                }
            }
            tex.Apply();

            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        public static void DrawButton(float windowWidth, ref float currentY, string text, System.Action onClick)
        {
            float buttonWidth = windowWidth - 2 * 10f;
            Rect rect = new Rect(10f, currentY, buttonWidth, 30f);
            bool clicked = GUI.Button(rect, text);
            if (clicked && onClick != null)
                onClick();
            currentY += 30f + 10f;
        }

        public static void DrawLabel(float windowWidth, ref float currentY, string text)
        {
            float labelWidth = windowWidth - 2 * 10f;
            Rect rect = new Rect(10f, currentY, labelWidth, 30f);
            GUI.Label(rect, text);
            currentY += 30f + 10f;
        }

        public static void DrawLabelTextField(float windowWidth, ref float currentY, string labelText, ref string text, int maxLength, float labelWidthFactor = 0.4f)
        {
            float totalWidth = windowWidth - 2 * 10f;
            float labelWidth = totalWidth * labelWidthFactor;
            float textFieldWidth = totalWidth - labelWidth - 10f;
            Rect labelRect = new Rect(10f, currentY, labelWidth, 30f);
            GUI.Label(labelRect, labelText);
            Rect textFieldRect = new Rect(10f + labelWidth + 10f, currentY, textFieldWidth, 30f);
            text = GUI.TextField(textFieldRect, text, maxLength);
            currentY += 30f + 10f;
        }

        public static void DrawToggle(float windowWidth, ref float currentY, string label, ref bool value, System.Action<bool> onToggle)
        {
            float buttonWidth = windowWidth - 2 * 10f;
            Rect rect = new Rect(10f, currentY, buttonWidth, 30f);

            Color originalColor = GUI.backgroundColor;
            if (value)
            {
                GUI.backgroundColor = Color.green;
                bool clicked = GUI.Button(rect, label + " [ON]");
                GUI.backgroundColor = originalColor;

                if (clicked)
                {
                    value = !value;
                    onToggle?.Invoke(value);
                }
            }
            else
            {
                bool clicked = GUI.Button(rect, label + " [OFF]");
                if (clicked)
                {
                    value = !value;
                    onToggle?.Invoke(value);
                }
            }

            currentY += 30f + 10f;
        }

        public static void DrawSeparator(float windowWidth, ref float currentY, float height = 2f)
        {
            Rect rect = new Rect(10f, currentY, windowWidth - 2 * 10f, height);
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            GUI.Box(rect, "");
            GUI.backgroundColor = originalColor;
            currentY += height + 10f;
        }

        public static bool DrawColoredButton(float windowWidth, ref float currentY, string text, Color color, System.Action onClick)
        {
            float buttonWidth = windowWidth - 2 * 10f;
            Rect rect = new Rect(10f, currentY, buttonWidth, 30f);

            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            bool clicked = GUI.Button(rect, text);
            GUI.backgroundColor = originalColor;

            if (clicked && onClick != null)
                onClick();

            currentY += 30f + 10f;
            return clicked;
        }
    }
=======
using UnityEngine;

namespace WalkSimulator.Bot
{
    public static class GUIHelper
    {
        public static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            result.filterMode = FilterMode.Bilinear;
            result.wrapMode = TextureWrapMode.Clamp;
            return result;
        }

        public static Texture2D MakeRoundedRectTexture(int width, int height, Color col, int roundPixels)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            Color clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = true;
                    if (x < roundPixels && y < roundPixels)
                    {
                        float dx = roundPixels - x;
                        float dy = roundPixels - y;
                        inside = (dx * dx + dy * dy) <= (roundPixels * roundPixels);
                    }
                    else if (x >= width - roundPixels && y < roundPixels)
                    {
                        float dx = x - (width - roundPixels);
                        float dy = roundPixels - y;
                        inside = (dx * dx + dy * dy) <= (roundPixels * roundPixels);
                    }
                    else if (x < roundPixels && y >= height - roundPixels)
                    {
                        float dx = roundPixels - x;
                        float dy = y - (height - roundPixels);
                        inside = (dx * dx + dy * dy) <= (roundPixels * roundPixels);
                    }
                    else if (x >= width - roundPixels && y >= height - roundPixels)
                    {
                        float dx = x - (width - roundPixels);
                        float dy = y - (height - roundPixels);
                        inside = (dx * dx + dy * dy) <= (roundPixels * roundPixels);
                    }
                    tex.SetPixel(x, y, inside ? col : clear);
                }
            }
            tex.Apply();

            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        public static void DrawButton(float windowWidth, ref float currentY, string text, System.Action onClick)
        {
            float buttonWidth = windowWidth - 2 * 10f;
            Rect rect = new Rect(10f, currentY, buttonWidth, 30f);
            bool clicked = GUI.Button(rect, text);
            if (clicked && onClick != null)
                onClick();
            currentY += 30f + 10f;
        }

        public static void DrawLabel(float windowWidth, ref float currentY, string text)
        {
            float labelWidth = windowWidth - 2 * 10f;
            Rect rect = new Rect(10f, currentY, labelWidth, 30f);
            GUI.Label(rect, text);
            currentY += 30f + 10f;
        }

        public static void DrawLabelTextField(float windowWidth, ref float currentY, string labelText, ref string text, int maxLength, float labelWidthFactor = 0.4f)
        {
            float totalWidth = windowWidth - 2 * 10f;
            float labelWidth = totalWidth * labelWidthFactor;
            float textFieldWidth = totalWidth - labelWidth - 10f;
            Rect labelRect = new Rect(10f, currentY, labelWidth, 30f);
            GUI.Label(labelRect, labelText);
            Rect textFieldRect = new Rect(10f + labelWidth + 10f, currentY, textFieldWidth, 30f);
            text = GUI.TextField(textFieldRect, text, maxLength);
            currentY += 30f + 10f;
        }

        public static void DrawToggle(float windowWidth, ref float currentY, string label, ref bool value, System.Action<bool> onToggle)
        {
            float buttonWidth = windowWidth - 2 * 10f;
            Rect rect = new Rect(10f, currentY, buttonWidth, 30f);

            Color originalColor = GUI.backgroundColor;
            if (value)
            {
                GUI.backgroundColor = Color.green;
                bool clicked = GUI.Button(rect, label + " [ON]");
                GUI.backgroundColor = originalColor;

                if (clicked)
                {
                    value = !value;
                    onToggle?.Invoke(value);
                }
            }
            else
            {
                bool clicked = GUI.Button(rect, label + " [OFF]");
                if (clicked)
                {
                    value = !value;
                    onToggle?.Invoke(value);
                }
            }

            currentY += 30f + 10f;
        }

        public static void DrawSeparator(float windowWidth, ref float currentY, float height = 2f)
        {
            Rect rect = new Rect(10f, currentY, windowWidth - 2 * 10f, height);
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            GUI.Box(rect, "");
            GUI.backgroundColor = originalColor;
            currentY += height + 10f;
        }

        public static bool DrawColoredButton(float windowWidth, ref float currentY, string text, Color color, System.Action onClick)
        {
            float buttonWidth = windowWidth - 2 * 10f;
            Rect rect = new Rect(10f, currentY, buttonWidth, 30f);

            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            bool clicked = GUI.Button(rect, text);
            GUI.backgroundColor = originalColor;

            if (clicked && onClick != null)
                onClick();

            currentY += 30f + 10f;
            return clicked;
        }
    }
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
} 