package dev.rehan.passthrough.client;

import com.google.gson.JsonObject;
import dev.rehan.passthrough.Passthrough;
import com.mojang.blaze3d.platform.InputConstants;
import com.mojang.blaze3d.platform.Window;
import dev.rehan.passthrough.WorldBridge;
import dev.rehan.passthrough.client.mixin.KeyMappingAccessor;
import dev.rehan.passthrough.client.mixin.MouseHandlerAccessor;
import net.minecraft.client.KeyMapping;
import net.minecraft.client.Minecraft;
import net.minecraft.client.MouseHandler;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.client.input.KeyEvent;
import net.minecraft.client.input.MouseButtonEvent;
import net.minecraft.client.input.MouseButtonInfo;
import net.minecraft.client.player.LocalPlayer;
import java.util.Locale;
import net.minecraft.core.component.DataComponents;
import net.minecraft.world.entity.EquipmentSlot;
import net.minecraft.world.item.component.ItemAttributeModifiers;
import net.minecraft.world.entity.ai.attributes.Attributes;
import net.minecraft.world.phys.HitResult;
import net.minecraft.world.entity.player.Inventory;
import org.lwjgl.sdl.SDLVideo;

/** Host input, applied on the client thread: the host window has the focus, so Minecraft never sees these itself. */
final class ClientInput {
	/** The button held since its press inside a screen (a drag), or null. */
	private static MouseButtonInfo dragging;
	private static double cursorX = -1, cursorY = -1;

	private ClientInput() {
	}

	/**
	 * The host's cursor at (nx, ny), 0..1 over the window. Minecraft only runs hover and drag for a focused window,
	 * and the host has the focus, so this does what MouseHandler.handleAccumulatedMovement would.
	 */
	private static void cursor(final Minecraft minecraft, final double nx, final double ny) {
		Window window = minecraft.getWindow();
		double x = nx * window.getScreenWidth(), y = ny * window.getScreenHeight();
		MouseHandlerAccessor mouse = (MouseHandlerAccessor)minecraft.mouseHandler;
		mouse.passthrough$setXpos(x);
		mouse.passthrough$setYpos(y);
		Screen screen = minecraft.gui.screen();
		if (screen != null && (x != cursorX || y != cursorY)) {
			double sx = MouseHandler.getScaledXPos(window, x), sy = MouseHandler.getScaledYPos(window, y);
			screen.mouseMoved(sx, sy);
			if (dragging != null && cursorX >= 0) {
				screen.mouseDragged(new MouseButtonEvent(sx, sy, dragging),
					MouseHandler.getScaledXPos(window, x - cursorX), MouseHandler.getScaledYPos(window, y - cursorY));
			}
			screen.afterMouseMove();
		}
		cursorX = x;
		cursorY = y;
	}

	static void handle(final Minecraft minecraft, final JsonObject m) {
		LocalPlayer player = minecraft.player;
		switch (m.get("t").getAsString()) {
			case "key" -> {
				String k = m.get("k").getAsString();
				boolean down = !m.has("down") || m.get("down").getAsBoolean();
				if (k.equals("escape")) {
					if (down && minecraft.gui.screen() != null) {
						minecraft.gui.screen().onClose();
					}

					return;
				}

				KeyMapping key = switch (k) {
					case "use" -> minecraft.options.keyUse;
					case "attack" -> minecraft.options.keyAttack;
					case "pick" -> minecraft.options.keyPickItem;
					case "inventory" -> minecraft.options.keyInventory;
					case "drop" -> minecraft.options.keyDrop;
					case "swap" -> minecraft.options.keySwapOffhand;
					default -> null;
				};
				if (k.equals("attack") && down && player != null
					&& (minecraft.hitResult == null || minecraft.hitResult.getType() != HitResult.Type.ENTITY)) {
					// a swing at no Minecraft entity: the host hits what's in front of Steve in its own world, for the
					// damage Minecraft would deal (the held item's attack damage, scaled by the attack cooldown's charge;
					// read before Minecraft handles the click and resets it)
					float charge = player.getAttackStrengthScale(0.5F);
					// (computed from the held item: the client never learns its attack_damage attribute, the server does that)
					double attack = player.getMainHandItem().getOrDefault(DataComponents.ATTRIBUTE_MODIFIERS, ItemAttributeModifiers.EMPTY)
						.compute(Attributes.ATTACK_DAMAGE, player.getAttributeBaseValue(Attributes.ATTACK_DAMAGE), EquipmentSlot.MAINHAND);
					double damage = attack * (0.2 + charge * charge * 0.8);
					Passthrough.events.accept(String.format(Locale.ROOT, "{\"t\":\"melee\",\"dmg\":%.2f}", damage));
				}

				if (key != null) {
					if (down && !key.isDown()) {
						KeyMappingAccessor access = (KeyMappingAccessor)key;
						access.passthrough$setClickCount(access.passthrough$getClickCount() + 1);
					}

					key.setDown(down);
				}
			}
			case "slot" -> {
				if (player != null) {
					player.getInventory().setSelectedSlot(Math.clamp(m.get("n").getAsInt(), 0, Inventory.getSelectionSize() - 1));
				}
			}
			case "scroll" -> {
				if (player != null) {
					Inventory inventory = player.getInventory();
					int size = Inventory.getSelectionSize();
					inventory.setSelectedSlot(Math.floorMod(inventory.getSelectedSlot() - m.get("d").getAsInt(), size));
				}
			}
			case "hud" -> {
				if (minecraft.gui.hud.isHidden() != m.get("hidden").getAsBoolean()) {
					minecraft.gui.hud.toggle();
				}
			}
			// The host's real keyboard and, while a screen is open, its cursor: fed through Minecraft's own input
			// handlers, so every keybind (inventory, chat, F3 combos, the game-mode switcher) works as if typed here.
			case "kbd" -> {
				int sc = m.get("sc").getAsInt(), action = m.get("a").getAsInt();
				if (sc == InputConstants.KEY_F4 && action == 1 && minecraft.gui.screen() == null && player != null
					&& !minecraft.options.keyDebugModifier.isDown()) {
					// F4 alone: creative <-> survival in one press (held F3 + F4 is still Minecraft's switcher)
					WorldBridge.command("gamemode " + (player.isCreative() ? "survival" : "creative") + " @a");
					return;
				}

				minecraft.keyboardHandler.keyPress(minecraft.getWindow().handle(), action,
					new KeyEvent(sc, m.get("kc").getAsInt(), m.get("m").getAsInt()));
			}
			case "text" -> minecraft.keyboardHandler.textInput(minecraft.getWindow().handle(), m.get("s").getAsString());
			case "mpos" -> cursor(minecraft, m.get("x").getAsDouble(), m.get("y").getAsDouble());
			case "mbtn" -> {
				cursor(minecraft, m.get("x").getAsDouble(), m.get("y").getAsDouble());
				boolean down = m.get("down").getAsBoolean();
				MouseButtonInfo button = new MouseButtonInfo(m.get("b").getAsInt(), m.get("m").getAsInt());
				dragging = down ? button : null;
				if (minecraft.gui.screen() != null) {
					minecraft.mouseHandler.onButton(minecraft.getWindow().handle(), button, down ? 1 : 0);
				}
			}
			case "mscroll" -> {
				if (minecraft.gui.screen() != null) {
					minecraft.mouseHandler.onScroll(minecraft.getWindow().handle(), 0, m.get("d").getAsDouble());
				}
			}
			case "view" -> {
				// match the host's picture exactly: un-minimize/un-maximize first (resizing a maximized window is ignored)
				int w = m.get("w").getAsInt(), h = m.get("h").getAsInt();
				long handle = minecraft.getWindow().handle();
				SDLVideo.SDL_RestoreWindow(handle);
				minecraft.getWindow().setWindowed(w, h);
				SDLVideo.SDL_SetWindowSize(handle, w, h);
				SDLVideo.SDL_SyncWindow(handle);
			}
			default -> {
			}
		}
	}
}
