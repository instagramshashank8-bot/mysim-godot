# Driving controls redesign

The old controls used opaque generic text boxes that covered the racing view. Steering and pedals had the same visual weight; button text dominated the car. Full-size Monoposto frames show thin translucent control silhouettes and a more legible road/car hierarchy. MySim keeps its own midnight/teal identity.

The replacement uses two circular chevron thumb pads, an elongated throttle pedal, a wider amber-accent brake pedal, and compact icon/text pit/view/pause actions. Touch regions remain at least72physical pixels in the960x540capture. Pedal shapes remain distinct without colour; held state gains an outline and translucent fill. Original independent-finger mapping is preserved. Focus loss clears visual and logical held state.

This is a source implementation awaiting actual cloud capture review, not final visual acceptance. Current functional touch regression41092515420passed all three resolutions before this visual change. Review the new captures against actual gameplay, not just an empty test surface.

User refinement: crisp material/UI definition; avoid creamy/smudged presentation. Controls are drawn as native vectors. Bloom now defaults off (explicit saved bloom option can enable it); no motion blur or full-screen softening added. Video compression and source texture resolution cannot be repaired by exaggerated sharpening. Full gameplay captures still require review.
