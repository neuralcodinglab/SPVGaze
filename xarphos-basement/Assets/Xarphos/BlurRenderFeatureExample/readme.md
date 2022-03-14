A simple render feature and a compute shader to grab the scene color after rendering and downscaling + blurring it for use in UI.

![](https://i.imgur.com/dZaOCcH.png)

Uses canvas stacking in order for this to work. The canvas that uses the blurring effect needs a lower sorting order than the other canvases in the scene.
If it doesnt have a lower sorting order it will simply overwrite the other canvases.

Downscaling the grabbed scene color texture before blurring it is recommended for performance, but reduces the quality somewhat.
Even a 2x downscale is a huge increase in performance, but tweak both that and the blur iterations to find a good middleground.

It's currently not working great with scene view, and you'll probably need a different method of approach for this to work nicely.
Other approaches uses camera stacking and render textures to work, but that does require a bit more setup.

## How To
1. Add the "UIBlurFeature" to your renderer data
2. Add the "BlurEffect.compute" to the feature settings "Effect Compute"
3. A texture named "_UIBlurTexture" is now exposed to your shaders
4. Add a secondary canvas and set "Sort Order" to be lower than all your other canvases, if one of your canvases dissappear you need a lower sort order
5. Use the attached UIBlur shader on a RawImage in the new canvas you created
6. Anything in the scene under the RawImage should now be blurred, including transparent objects

## Issues
- The effect will affect any Canvases in the "Screen Space - Camera/World" mode
- It doesnt work properly in scene view
- Requires a separate canvas for any blurring effect
- The texture wont work for objects in the scene, see https://gist.github.com/Refsa/54da34a9e2fc8e45472286572216ad17 for a workaround

MIT License

Copyright (c) 2020 Refsa

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.