# I tried to fix versions as much as possible to reduce the risk of build failure or other future issues, but some are missing
# This base image should be enough to run any bot using OmniBot libraries. It includes:
# - .NET 7.0 (.NET 8.0 can fail because of OpenSSL 1 missing)
# - FFmpeg 4.3.6 (for SIPSorcery)
# - build-essential (to build llama.cpp)
# - libopus and libsodium (for Discord)
# - Python 3 with:
#   - pip
#   - libpython (for PythonNet)
#   - Pyro4 (for Flame, Python remote access)
#   - llama-cpp-python
#   - guidance

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base

RUN echo "deb http://snapshot.debian.org/archive/debian/20220419T024549Z stable main" > /etc/apt/sources.list.d/snapshots.list \
 && apt-get -o Acquire::Check-Valid-Until=false update \
 && apt-get install -y --no-install-recommends \
      ffmpeg=7:4.3.6-0+deb11u1 \
      python3 python3-pip python3-dev \
      libopus-dev libsodium-dev \
      build-essential \
 && apt-get clean \
 && rm -rf /var/lib/apt/lists/*

RUN pip3 install Pyro4==4.82 guidance==0.1.10
RUN CMAKE_ARGS="-DLLAMA_OPENBLAS=on" pip3 install llama-cpp-python==0.2.38

ENV OMNIBOT__FFMPEGLIBRARYPATH=/usr/lib/x86_64-linux-gnu
ENV OMNIBOT__PYTHONLIBRARYPATH=/usr/lib/x86_64-linux-gnu/libpython3.9.so

WORKDIR /app
