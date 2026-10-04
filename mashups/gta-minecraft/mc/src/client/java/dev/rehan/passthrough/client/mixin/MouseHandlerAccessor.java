package dev.rehan.passthrough.client.mixin;

import net.minecraft.client.MouseHandler;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;

/** The cursor position (window pixels) the host sets while a screen is open: Minecraft's window never has focus. */
@Mixin(MouseHandler.class)
public interface MouseHandlerAccessor {
	@Accessor("xpos")
	void passthrough$setXpos(double x);

	@Accessor("ypos")
	void passthrough$setYpos(double y);
}
