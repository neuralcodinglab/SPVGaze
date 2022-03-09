#ifndef XRP_PHOSPHENE_VISION
    #define XRP_PHOSPHENE_VISION
    

    // Pseudo-random noise generators 
    uint xrp_wang_hash(uint key)
    {
        uint hash = (key ^ 61) ^(key >> 16);
        
        hash += hash << 3;
        hash ^= hash >> 4;
        hash *= 0x27d4eb2d;
        hash ^= hash >> 15;
        
        return hash;
    }

        
    float xrp_white_noise(uint2 seed)
    {
        return (xrp_wang_hash(321 * xrp_wang_hash(seed.x) + 123 * seed.y) / 4294967296.0);
    }

    float2 xrp_random_vector(uint2 seed)
    {
        float2 result;
        result.x = xrp_white_noise(666+seed)-0.5;
        result.y = xrp_white_noise(007+seed)-0.5;
        return result;
    }

    fixed4 xrp_random_color(uint2 seed)
    {
        fixed4 result = fixed4(1,1,1,1);
        result.r = 0.5*xrp_white_noise(777+seed);
        result.b = 0.5*xrp_white_noise(999+seed);
        result.g = 0.5*xrp_white_noise(142+seed);
        return result;
    }


    fixed4 xrp_gaussian(sampler2D mask, float2 resolution, float2 base_pos, float2 offset_pos, float2 intensity_params, float2 size_params, float4 noise_params)
    {   
    /* This function calculates the pixel color and intensity as function of the relative distance to
    the gridpoint ('offset_pos'). The location of the gridpoint ('base_pos') is used to sample the phosphene
    activation mask and as a random seed to give each phosphene some unique characteristics such as: color, 
    noise (positional jitter, intensity variation, size variation,dropout), size (cortical magnification). */
    
    // Positional jitter
    float2 jitter = xrp_random_vector(base_pos*resolution)*noise_params.x/resolution;
    base_pos += jitter;
    offset_pos -= jitter;

    // Phosphene size as function of base position ((initial size + cortical magnification) times variation factor)
    float magnification = size_params.y*sqrt((base_pos.x-0.5)*(base_pos.x-0.5)+(base_pos.y-0.5)*(base_pos.y-0.5)); // factor that increases linearly with eccentricity
    float size_var_factor = 1.0+noise_params.z*(xrp_white_noise(base_pos*resolution)-0.5); // Value around 1
    float size = size_var_factor*(size_params.x+magnification)/resolution; 
 
    // Phosphene color/intensity
    fixed4 color = (1-intensity_params.y)*fixed4(1,1,1,1) + intensity_params.y*xrp_random_color(base_pos*resolution); // intensity_params.y determines the color saturation
    float intensity_var = 1.0+noise_params.y*(xrp_white_noise(base_pos*resolution)-0.5);
    float intensity =  intensity_params.x * intensity_var;// * color(base_pos)

    // Phosphene activation (sample value of phosphene activation mask at base position)
    float activation = Luminance(tex2D(mask,base_pos)); //1: active phosphene   0:inactive phosphene.
    float dropout = max(sign(xrp_white_noise(base_pos*resolution)-noise_params.w), 0.0); //0: dropped out, 1: working
    activation *= dropout;

    // Pixel luminance follows gaussian according to offset w.r.t. base position
    float r = sqrt(offset_pos.x * offset_pos.x + offset_pos.y * offset_pos.y); // r = distance to grid point 
    fixed4 pixel = intensity * color * activation *exp((r*r)/(-2.0 * size * size));
    return pixel;
    }


    fixed4 xrp_phosphene_filter(sampler2D mask, float2 position, float2 resolution, float2 intensity_params,float2 size_params,float4 noise_params)
    {
    /* This function calculates the pixel intensity at a given position, according to the phosphene activation
    of its surrounding phosphenes. For this purpose, the pixel position on a hexagonal grid is determined, and 
    then, by calling the xrp_gaussian() function multiple times, the pixel intensity is calculated additively 
    according to gaussian activation of it's surrounding phosphenes. 
    */
    
   
    // Create cartesian phosphene grid (and determine relative pixel position)
    float2 base_pos = (floor(resolution*position) + 0.5) / resolution;  // position on the grid 
    float2 offset_pos = (frac(resolution*position) - 0.5)/ resolution;  // pixel position relative to the base gridpoint position

    // Convert cartesian to hegagonal grid (shift odd rows with 0.5*resolution)
    float odd = frac(base_pos.y*0.5*resolution);
    base_pos.x += frac(base_pos.y*0.5*resolution)/resolution;
    offset_pos.x -= frac(base_pos.y*0.5*resolution)/resolution;

    // Calculate the pixel intensity (according to activation of (nearest) phosphene at base position)
    fixed4 pixel = xrp_gaussian(mask,resolution,base_pos,offset_pos,intensity_params,size_params,noise_params);
    
    // Move through neigbouring hexagons, for additive effect of different phosphenes
    // Add left neigbour 
    base_pos.x -= 1/resolution;
    offset_pos.x += 1/resolution;
    pixel += xrp_gaussian(mask,resolution,base_pos,offset_pos,intensity_params,size_params,noise_params);

     // Add left neigbour 
    base_pos.x -= 1/resolution;
    offset_pos.x += 1/resolution;
    pixel += xrp_gaussian(mask,resolution,base_pos,offset_pos,intensity_params,size_params,noise_params);
    
    // Add bottom neigbour 
    base_pos.x += 0.5/resolution;
    offset_pos.x -= 0.5/resolution;
    base_pos.y -= 1/resolution;
    offset_pos.y += 1/resolution;
    pixel += xrp_gaussian(mask,resolution,base_pos,offset_pos,intensity_params,size_params,noise_params);

    // Add right neigbour 
    base_pos.x += 1/resolution;
    offset_pos.x -= 1/resolution;
    pixel += xrp_gaussian(mask,resolution,base_pos,offset_pos,intensity_params,size_params,noise_params);

    // Add right neigbour 
    base_pos.x += 1/resolution;
    offset_pos.x -= 1/resolution;
    pixel += xrp_gaussian(mask,resolution,base_pos,offset_pos,intensity_params,size_params,noise_params);

    // Add top neigbour 
    base_pos.x += 0.5/resolution;
    offset_pos.x -= 0.5/resolution;
    base_pos.y += 1/resolution;
    offset_pos.y -= 1/resolution;
    pixel += xrp_gaussian(mask,resolution,base_pos,offset_pos,intensity_params,size_params,noise_params);

    // Add top neigbour 
    base_pos.x -= 0.5/resolution;
    offset_pos.x += 0.5/resolution;
    base_pos.y += 1/resolution;
    offset_pos.y -= 1/resolution;
    pixel += xrp_gaussian(mask,resolution,base_pos,offset_pos,intensity_params,size_params,noise_params);

    // Add left neigbour 
    base_pos.x -= 1/resolution;
    offset_pos.x += 1/resolution;
    pixel += xrp_gaussian(mask,resolution,base_pos,offset_pos,intensity_params,size_params,noise_params);

    // Add left neigbour 
    base_pos.x -= 1/resolution;
    offset_pos.x += 1/resolution;
    pixel += xrp_gaussian(mask,resolution,base_pos,offset_pos,intensity_params,size_params,noise_params);
    
    return  pixel;
    }

#endif

 
