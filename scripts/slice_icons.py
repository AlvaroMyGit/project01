import sys
import os
try:
    from PIL import Image
except ImportError:
    print("Please install Pillow: pip install Pillow")
    sys.exit(1)

def slice_spritesheet(sheet_path, output_dir, tile_w=50, tile_h=50):
    try:
        sheet = Image.open(sheet_path)
    except Exception as e:
        print(f"Failed to open {sheet_path}: {e}")
        return
        
    width, height = sheet.size
    os.makedirs(output_dir, exist_ok=True)
    
    cols = width // tile_w
    rows = height // tile_h
    
    count = 0
    for row in range(rows):
        for col in range(cols):
            left = col * tile_w
            top = row * tile_h
            right = left + tile_w
            bottom = top + tile_h
            
            # Crop
            tile = sheet.crop((left, top, right, bottom))
            
            # Optional: check if tile is fully transparent here
            # For now, extract all grids
            out_name = f"icon_{col:02d}_{row:02d}.png"
            tile.save(os.path.join(output_dir, out_name), format="PNG")
            count += 1
            
    print(f"Sliced {count} icons to {output_dir}")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python slice_icons.py <spritesheet_path> <output_dir> [tile_w=50] [tile_h=50]")
        sys.exit(1)
        
    sheet_path = sys.argv[1]
    out_dir = sys.argv[2]
    tw = int(sys.argv[3]) if len(sys.argv) > 3 else 50
    th = int(sys.argv[4]) if len(sys.argv) > 4 else 50
    
    slice_spritesheet(sheet_path, out_dir, tw, th)
