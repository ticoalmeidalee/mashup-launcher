package dev.rehan.passthrough;

import java.util.ArrayList;
import java.util.List;
import net.fabricmc.fabric.api.creativetab.v1.CreativeModeTabEvents;
import net.minecraft.core.Registry;
import net.minecraft.core.particles.ParticleTypes;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.core.registries.Registries;
import net.minecraft.resources.Identifier;
import net.minecraft.resources.ResourceKey;
import net.minecraft.sounds.SoundEvent;
import net.minecraft.sounds.SoundEvents;
import net.minecraft.sounds.SoundSource;
import net.minecraft.world.InteractionHand;
import net.minecraft.world.InteractionResult;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.item.CreativeModeTabs;
import net.minecraft.world.item.Item;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.ItemUseAnimation;
import net.minecraft.world.level.Level;
import net.minecraft.world.phys.Vec3;

/**
 * GTA's guns as Minecraft items: Steve holds a blocky Minecraft gun, and each shot is fired by the host as a real
 * GTA bullet ({"t":"shoot","w":id}) from Steve towards the crosshair, with that weapon's damage, impacts and
 * wanted level. Minecraft does the hand, the sound, the smoke and the cooldown bar.
 */
public final class Guns {
	/** One gun: ticks between shots, held to fire (auto) or one shot per click, and its Minecraft sound. */
	public record Spec(String id, int interval, boolean auto, SoundEvent sound, float pitch, float volume) {
	}

	public static final List<Spec> SPECS = List.of(
		new Spec("pistol", 5, false, SoundEvents.FIREWORK_ROCKET_BLAST, 1.6F, 0.9F),
		new Spec("smg", 2, true, SoundEvents.FIREWORK_ROCKET_BLAST, 1.9F, 0.6F),
		new Spec("assault_rifle", 3, true, SoundEvents.FIREWORK_ROCKET_LARGE_BLAST, 1.5F, 0.7F),
		new Spec("pump_shotgun", 18, false, SoundEvents.FIREWORK_ROCKET_LARGE_BLAST, 0.8F, 1.0F),
		new Spec("sniper_rifle", 30, false, SoundEvents.FIREWORK_ROCKET_LARGE_BLAST, 0.6F, 1.0F),
		new Spec("rpg", 40, false, SoundEvents.FIREWORK_ROCKET_LAUNCH, 0.6F, 1.0F));
	public static final List<Item> ITEMS = new ArrayList<>();

	private Guns() {
	}

	static void register() {
		for (Spec spec : SPECS) {
			ResourceKey<Item> key = ResourceKey.create(Registries.ITEM, Identifier.fromNamespaceAndPath(Passthrough.ID, spec.id()));
			ITEMS.add(Registry.register(BuiltInRegistries.ITEM, key, new GunItem(new Item.Properties().setId(key).stacksTo(1), spec)));
		}

		CreativeModeTabEvents.modifyOutputEvent(CreativeModeTabs.COMBAT).register(output -> ITEMS.forEach(output::accept));
	}

	public static boolean isGun(final ItemStack stack) {
		return stack.getItem() instanceof GunItem;
	}

	public static final class GunItem extends Item {
		private final Spec spec;

		GunItem(final Item.Properties properties, final Spec spec) {
			super(properties);
			this.spec = spec;
		}

		@Override
		public InteractionResult use(final Level level, final Player player, final InteractionHand hand) {
			ItemStack stack = player.getItemInHand(hand);
			if (this.spec.auto()) {
				player.startUsingItem(hand); // onUseTick fires while the button stays down
				return InteractionResult.CONSUME;
			}

			this.fire(level, player);
			player.getCooldowns().addCooldown(stack, this.spec.interval());
			return InteractionResult.CONSUME;
		}

		@Override
		public void onUseTick(final Level level, final LivingEntity entity, final ItemStack stack, final int ticksRemaining) {
			if ((this.getUseDuration(stack, entity) - ticksRemaining) % this.spec.interval() == 0) {
				this.fire(level, entity);
			}
		}

		@Override
		public int getUseDuration(final ItemStack stack, final LivingEntity user) {
			return 72000;
		}

		@Override
		public ItemUseAnimation getUseAnimation(final ItemStack stack) {
			return ItemUseAnimation.NONE;
		}

		/** Client side only: the host fires the real bullet; Minecraft plays the shot. */
		private void fire(final Level level, final LivingEntity shooter) {
			if (!level.isClientSide()) {
				return;
			}

			Passthrough.events.accept("{\"t\":\"shoot\",\"w\":\"" + this.spec.id() + "\"}");
			level.playLocalSound(shooter, this.spec.sound(), SoundSource.PLAYERS, this.spec.volume(), this.spec.pitch());
			Vec3 look = shooter.getLookAngle();
			Vec3 muzzle = shooter.getEyePosition().add(look.scale(0.9)).add(0, -0.25, 0);
			level.addParticle(ParticleTypes.SMOKE, muzzle.x, muzzle.y, muzzle.z, look.x * 0.05, 0.02, look.z * 0.05);
			level.addParticle(ParticleTypes.CRIT, muzzle.x, muzzle.y, muzzle.z, look.x * 0.4, look.y * 0.4, look.z * 0.4);
		}
	}
}
