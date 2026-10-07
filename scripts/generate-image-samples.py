"""Run on the rented GPU. Drafts only; save seeds and source versions beside PNGs."""
import json
from pathlib import Path
import torch
from diffusers import StableDiffusionXLPipeline, AutoencoderKL
from huggingface_hub import HfApi

out = Path('/workspace/riakawa/outputs/images')
out.mkdir(parents=True, exist_ok=True)
models = ['stabilityai/stable-diffusion-xl-base-1.0', 'nerijs/pixel-art-xl', 'madebyollin/sdxl-vae-fp16-fix']
revisions = {name: HfApi().model_info(name).sha for name in models}
vae = AutoencoderKL.from_pretrained(models[2], revision=revisions[models[2]], torch_dtype=torch.float16)
pipe = StableDiffusionXLPipeline.from_pretrained(models[0], revision=revisions[models[0]],
    vae=vae, torch_dtype=torch.float16, variant='fp16', use_safetensors=True)
pipe.load_lora_weights(models[1], revision=revisions[models[1]])
pipe.to('cuda')
pipe.enable_vae_slicing()
negative = '3d render, photograph, realistic, blurry, text, watermark, letters, scenery, shadow, gradient, human, extra limbs, clothes'
prompts = {
    'chiikawa-idle-v2': 'pixel art, Chiikawa, tiny round white creature, small round ears, black oval eyes, w mouth, pink cheeks, tiny arms, two short feet, no tail, no clothing, facing right, full body, solid green background, dark outline, 32 pixel sprite',
    'chiikawa-fork-v2': 'pixel art, pink sasumata, U shaped fork head, two round prongs, long straight pink pole, diagonal, isolated item, green background, dark outline, flat colors, 32 pixel sprite',
}
for name, prompt in prompts.items():
    for seed in (42, 777):
        path = out / f'{name}-{seed}.png'
        if path.exists(): continue
        image = pipe(prompt=prompt, negative_prompt=negative, height=768, width=768,
            num_inference_steps=30, guidance_scale=6.5,
            generator=torch.Generator('cuda').manual_seed(seed)).images[0]
        image.save(path)
        path.with_suffix('.json').write_text(json.dumps({'prompt': prompt, 'negative': negative,
            'seed': seed, 'steps': 30, 'guidance': 6.5, 'models': revisions, 'status': 'draft'}, indent=2))
        print('GENERATED', path, flush=True)
