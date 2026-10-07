#!/usr/bin/env bash
set -euo pipefail
mkdir -p /workspace/riakawa/{outputs,models,logs}
python -m pip install uv
uv venv --system-site-packages /workspace/riakawa/image-env
uv pip install --python /workspace/riakawa/image-env/bin/python \
  'diffusers==0.37.0' 'transformers==4.57.6' accelerate peft sentencepiece protobuf soundfile \
  'torch==2.7.1' 'torchvision==0.22.1' 'torchaudio==2.7.1' \
  --extra-index-url https://download.pytorch.org/whl/cu128 --index-strategy unsafe-best-match
echo IMAGE_ENV_READY
uv venv /workspace/riakawa/music-env --python 3.11
uv pip install --python /workspace/riakawa/music-env/bin/python 'huggingface-hub==0.36.2'
/workspace/riakawa/music-env/bin/hf download m-a-p/YuE2-3B \
  yue2_infer-0.1.5-py3-none-any.whl --local-dir /workspace/riakawa/models
uv pip install --python /workspace/riakawa/music-env/bin/python \
  /workspace/riakawa/models/yue2_infer-0.1.5-py3-none-any.whl
echo MUSIC_ENV_READY
