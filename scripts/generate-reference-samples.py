"""Z-Image reference trials after the SDXL pixel LoRA failed character fidelity."""
import json
from pathlib import Path
import torch
from diffusers import ZImagePipeline
from huggingface_hub import HfApi, snapshot_download

model = 'Tongyi-MAI/Z-Image-Turbo'
revision = HfApi().model_info(model).sha
snapshot_download(model, revision=revision)
pipe = ZImagePipeline.from_pretrained(model, revision=revision, torch_dtype=torch.bfloat16)
pipe.enable_model_cpu_offload()
out = Path('/workspace/riakawa/outputs/references')
out.mkdir(parents=True, exist_ok=True)
prompts = {
    'chiikawa': 'A single 2D pixel art sprite of Chiikawa (吉伊卡哇, ちいかわ), the little white character drawn by Nagano. '
        'Recognizable original character design: a very round white head merging into a short chubby white body, '
        'two very small rounded white ears on top, two solid black oval eyes with tiny white highlights, '
        'a tiny black w shaped mouth, pink blush marks on both cheeks, two very short white arms and two stubby feet. '
        'No nose, no whiskers, no tail, no clothing. Cheerful and shy. Three-quarter side view facing right. '
        'The full character is visible and centered with ample empty margin. A game sprite for Terraria, '
        'drawn on a coarse 40 by 40 pixel grid and enlarged with nearest-neighbor, crisp square pixels, '
        'flat white and blush pink colors, a thin dark brown outline, no shading or gradients. '
        'Completely plain solid bright green background, no scenery, no shadow, no text, one character only.',
    'chiikawa-fork': 'A single isolated 2D pixel art inventory icon of Chiikawa\'s pink sasumata weapon. '
        'A simple long straight pastel pink pole with a symmetrical U-shaped two-pronged pink fork at the top. '
        'Exactly two short round prongs point upward; a wide empty gap lies between the prongs. '
        'Not a sword, not a trident, no blade, no decorations, no character. '
        'The pole lies diagonally from the bottom left to the top right, full weapon visible with empty margin. '
        'A Terraria inventory sprite drawn at 32 by 32 pixels enlarged with nearest-neighbor. '
        'Crisp square pixels, three pink colors, a thin dark brown outline. '
        'Plain solid bright green background, one object only, no shadow, no text.'
}
for name, prompt in prompts.items():
    for seed in (42, 777):
        path = out / f'{name}-{seed}.png'
        if path.exists():
            continue
        image = pipe(prompt=prompt, height=1024, width=1024, num_inference_steps=9,
            guidance_scale=0, generator=torch.Generator('cuda').manual_seed(seed)).images[0]
        image.save(path)
        path.with_suffix('.json').write_text(json.dumps({'model':model, 'revision':revision,
            'prompt':prompt,'seed':seed,'steps':9,'status':'draft'}, indent=2))
        print('REFERENCE_READY', name, seed, flush=True)
