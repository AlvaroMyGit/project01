import sys
import os
try:
    from rembg import remove
    from PIL import Image
except ImportError:
    print("Please install requirements: pip install rembg Pillow")
    sys.exit(1)

def process_sprite(input_path, output_path):
    try:
        # Load image
        input_img = Image.open(input_path)
        
        # Remove background
        bg_removed = remove(input_img)
        
        # Nearest neighbor resize to 32x32
        resized = bg_removed.resize((32, 32), Image.Resampling.NEAREST)
        
        # Save
        resized.save(output_path, format="PNG")
        print(f"Processed: {output_path}")
    except Exception as e:
        print(f"Failed to process {input_path}: {e}")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python process_sprites.py <input_dir> <output_dir>")
        sys.exit(1)
        
    input_dir = sys.argv[1]
    output_dir = sys.argv[2]
    
    os.makedirs(output_dir, exist_ok=True)
    
    for filename in os.listdir(input_dir):
        if filename.lower().endswith(('.png', '.jpg', '.jpeg')):
            in_path = os.path.join(input_dir, filename)
            out_path = os.path.join(output_dir, f"{os.path.splitext(filename)[0]}.png")
            process_sprite(in_path, out_path)
