#!/usr/bin/bash

find . -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} +
