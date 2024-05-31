## Experimental code for VR phosphene simulations with gaze-contingent image processing
This repository contains the experimental scripts for the publication: de Ruyter van Steveninck, J., Nipshagen, M., van Gerven, M., Güçlü, U., Güçlüturk, Y., & van Wezel, R. (2024). Gaze-contingent processing improves mobility, scene recognition and visual search in simulated head-steered prosthetic vision. Journal of neural engineering, 21(2), Article 026037. https://doi.org/10.1088/1741-2552/ad357d

Note that this repository only contains the experimental scripts. The data-analysis can be found here: https://github.com/neuralcodinglab/SPVGazeAnalysis/tree/main

# Remarks
The code is specific to our experimental setup which used the HTC VIVE Pro Eye headset, and 3D virtual environments designed by ArchVizPRO studios (which you have to buy in the Unity asset store: https://assetstore.unity.com/publishers/13358)

The following libraries are excluded from the upload and need to be added locally:
- OpenCV for Unity [From Unity Asset Store](https://assetstore.unity.com/packages/tools/integration/opencv-for-unity-21088)
- ViveFoveatedRendering [From Unity Asset Store](https://assetstore.unity.com/packages/tools/particles-effects/vive-foveated-rendering-145635)
- ViveSR [FAQ & Overview](https://forum.vive.com/topic/5641-sranipal-faq/)


Branches 'experiment' and 'experiment 2' contain the code that was used for the simulated prosthetic vision experiments. Experiment 1 and 2, respectively, tested mobility (obstacle avoidance) and orientation (scene recognition and visual search) with simulated prosthetic vision. 
