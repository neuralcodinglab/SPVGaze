#ifndef DSPV_PHOSPHENE_VISION
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
#pragma exclude_renderers d3d11 gles
// // Upgrade NOTE: excluded shader from DX11 because it uses wrong array syntax (type[size] name)
// #pragma exclude_renderers d3d11
    #define DSPV_PHOSPHENE_VISION


    // Pseudo-random noise generators (not used for now)
    uint umu7_wang_hash(uint key)
    {
        uint hash = (key ^ 61) ^(key >> 16);

        hash += hash << 3;
        hash ^= hash >> 4;
        hash *= 0x27d4eb2d;
        hash ^= hash >> 15;

        return hash;
    }

    fixed4 DSPV_gaussian(float r, float sigma) {
        float c = 1 / (sigma * 3.5449077);
        return c * exp(-(r * r) / (2 * sigma * sigma));
    }


    float4 DSPV_phospheneSimulation(int gazeLocked, float2 eyePosition, float4 pSpecs[1000], float activation[1000], float nPhosphenes, float sizeCoefficient, float brightness, float dropout, float2 pixelPosition) {
        // Output luminance for current pixel
        fixed4 pixelLuminance = fixed4(0,0,0,0);

        // Distance of current pixel to phosphene center
        float phospheneDistance;

        // Pre-specified phosphene characteristics
        fixed4 phospheneSpecs;
        float2 phospheneCenter;
        float phospheneSize;


        // Loop over all phosphenes
        for (int i = 0; i<nPhosphenes; i++){

          // // Previous solution:
          // For each phosphene, read specs from pMapping texture
          // phospheneSpecs = tex2D(phospheneMapping,i/(float)nPhosphenes);
          // phospheneCenter = phospheneSpecs.rg;
          // phospheneSize = phospheneSpecs.b * sizeCoefficient;


          /// Read predifined phosphene specifiations from float4 array
          phospheneSpecs = pSpecs[i];
          phospheneSize = phospheneSpecs.b * sizeCoefficient;
          phospheneCenter = phospheneSpecs.rg;
          if (gazeLocked == 1){
            phospheneCenter += eyePosition -0.5;
          }

          // Calculate distance to current pixel (only the activity of nearby
          // phosphenes have an effect on the current pixel intensity)
          phospheneDistance = distance(phospheneCenter,pixelPosition);
          if (phospheneDistance < 3 * phospheneSize) {
            // Add the effect of the phosphene to the luminance of the current pixel
            pixelLuminance += activation[i]  * DSPV_gaussian(phospheneDistance, phospheneSize);
          }
        }

        // Fixation dot is colored red
        if (distance(pixelPosition, eyePosition)<0.01) {
            pixelLuminance.r = 1/brightness;
        }

        return brightness * pixelLuminance; //   tex2D(phospheneMapping,xgrid +(0.5/36)); //
    }

#endif
