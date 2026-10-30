# Physics teardown - current findings

This is a source-and-evidence audit, not complete physics acceptance.

## Corrected
- Dynamics handbrake previously entered total service force and then entered rear torque again. It now contributes one rear-axle force budget; normal service front/rear bias remains 58/42.
- ABS now modulates service torque separately from the mechanical rear handbrake.
- Independent tyre normal-load clamps could change the total available vertical force. Axle/side transfer now conserves mass*g+downforce, remains nonnegative and permits wheel unloading. Core conservation and mirror checks passed on GitHub41035128882. Broad native regression is running.
- Imported bridge deck support and barrier/soffit faces retain their original triangle union with distinct contact roles. All24models passed MonzaJunior41025895293.
- Deep confirmed roof overlaps stay blocked when bounded planar separation finds no safe exit. Roof/parapet checks remain mandatory.

## Measured unresolved behavior
- Proposed AI3 passed1434/1440nativecases: invalid laps for cars15/16/17/18/21 on layout54; one barrier contact for car20Monaco. Never enabled by default.
- AI4 fixes the two focused native regressions but introduces invalid logic laps for car23 on layouts28/51. It is also not enabled by default. Do not select controller variants by hardcoded car/track exceptions.

## Model limitations requiring further engineering
- Dynamics uses a constant .33m wheel radius and1.6kg*m2 wheel inertia for all cars. Recovered wheel assemblies differ; any calibration must use measured per-axle data, retain units and validate wheel speed, braking and energy together.
- Drive torque goes only to the rear wheels for every car. The data catalogue lacks a verified drivetrain distribution; do not claim AWD fidelity or guess torque splits from a car name.
- Engine force is power-limited from mass/0-60 calibration. Gear ratios are used for displayed gear/rpm, not a complete clutch/gearbox/torque-curve drivetrain.
- The driving dynamics are planar. Four-wheel ray samples orient the visual body, but this is not a full heave/pitch/roll suspension and airborne rigid-body solver.
- Current yaw inertia is mass*wheelbase^2*.28, track width and CG height are inferred. These must be calibrated against documented source data or controlled maneuvers rather than described as measured real-car properties.
- Tyre model is an empirical combined-slip curve with thermal/wear/wetness multipliers. It lacks validated tyre datasets, pressure/camber/transient relaxation behavior. Assertions can prove conservation and numerical bounds, not real-world authenticity.
- Assist damping and low-speed blending influence yaw; coast/step-steer, constant-radius, braking/reverse, wet/split-friction, traffic impact and timestep sensitivity need controlled recorded comparisons.
- Contact containment and reset behavior are separate from the tyre solver. No successful clean lap can certify collisions, full track barrier coverage, hardware controls or driving feel.

## Running verification
Source7ff4beb physical-load/brake changes: owner41035182443cars16-23;Jdiejei41035191643cars0-7;blackwing41035202897cars8-15. Twelve track shards each;1440unique cases. Do not duplicate these jobs. Later5eb8595 only separates ABS/handbrake modulation and requires its core check.

All further simulation/render/build runs on GitHub. Original assets, UVs, source dimensions and failure evidence remain preserved.
