using UnityEngine;

namespace GravityWells.Game
{
    // The only sprite the game creates: an antialiased white disc drawn into a texture at boot and
    // tinted by code. Shared by every ball, UI dot and shadow.
    public static class CircleSprite
    {
        public static Sprite Create(int size = 128)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Vector2 center = new Vector2(size - 1, size - 1) * 0.5f;
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f); // ~1px antialiased edge
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            // pixelsPerUnit = size makes the sprite exactly 1 world unit across at scale 1.
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
