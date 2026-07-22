<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ChevronDown, X } from 'lucide-vue-next'
import type { SelectOption } from './types'

const { t } = useI18n()

/**
 * Searchable single-select (combobox). Drop-in replacement for the former native-`<select>` wrapper:
 * same `v-model` (option value) + `options` props, so every existing usage becomes searchable for
 * free. Click to open, type to filter a long list, ↑/↓/Enter/Esc to drive it by keyboard, ✕ to clear.
 * Styled to the app's field + command-palette idiom (no library dependency). Filtering is client-side.
 */
const model = defineModel<string>()

const props = withDefaults(
  defineProps<{
    options: SelectOption[]
    placeholder?: string
    disabled?: boolean
    /** Show the ✕ that resets the selection to empty. On by default. */
    clearable?: boolean
  }>(),
  { placeholder: '', disabled: false, clearable: true },
)

// A stable id per instance so the input can reference its listbox for assistive tech.
const uid = `appselect-${(AppSelectCounter.n += 1)}`

const open = ref(false)
const query = ref('')
const activeIndex = ref(0)
const rootEl = ref<HTMLElement | null>(null)
const inputEl = ref<HTMLInputElement | null>(null)
const listboxEl = ref<HTMLElement | null>(null)

// The popup is teleported to <body> and positioned with fixed coordinates, so it is never clipped by
// an ancestor's overflow (line-item tables, modal bodies) — the reason a plain absolute popup was cut
// off at the table edge.
const menuStyle = reactive<Record<string, string>>({})

const selected = computed(() => props.options.find((o) => o.value === model.value) ?? null)
const selectedLabel = computed(() => selected.value?.label ?? '')

const filtered = computed(() => {
  const q = query.value.trim().toLowerCase()
  if (!q) return props.options
  return props.options.filter((o) => o.label.toLowerCase().includes(q))
})

// What the input shows: the live query while open, otherwise the chosen label.
const displayValue = computed(() => (open.value ? query.value : selectedLabel.value))
// While open with an existing selection, keep it visible as the placeholder so context isn't lost.
const placeholderText = computed(() =>
  open.value && selectedLabel.value ? selectedLabel.value : props.placeholder,
)

function openMenu() {
  if (props.disabled) return
  open.value = true
  query.value = ''
  // Land the highlight on the current selection, else the first row.
  const i = filtered.value.findIndex((o) => o.value === model.value)
  activeIndex.value = i >= 0 ? i : 0
}

function closeMenu() {
  open.value = false
  query.value = ''
}

function onInput(e: Event) {
  query.value = (e.target as HTMLInputElement).value
  open.value = true
  activeIndex.value = 0
}

function choose(opt: SelectOption) {
  model.value = opt.value
  closeMenu()
  inputEl.value?.blur()
}

function clear() {
  model.value = ''
  query.value = ''
  open.value = true
  nextTick(() => inputEl.value?.focus())
}

function move(delta: number) {
  if (!open.value) {
    openMenu()
    return
  }
  const n = filtered.value.length
  if (n === 0) return
  activeIndex.value = (activeIndex.value + delta + n) % n
  scrollActiveIntoView()
}

function selectActive() {
  if (!open.value) {
    openMenu()
    return
  }
  const opt = filtered.value[activeIndex.value]
  if (opt) choose(opt)
}

function scrollActiveIntoView() {
  nextTick(() => {
    const el = listboxEl.value?.children[activeIndex.value] as HTMLElement | undefined
    el?.scrollIntoView({ block: 'nearest' })
  })
}

// Position the floating popup under (or, when short on space, over) the field, matching its width.
function updatePosition() {
  const el = inputEl.value
  if (!el) return
  const r = el.getBoundingClientRect()
  const gap = 4
  const maxH = 256
  const spaceBelow = window.innerHeight - r.bottom - gap - 8
  const spaceAbove = r.top - gap - 8
  const openUp = spaceBelow < 180 && spaceAbove > spaceBelow

  menuStyle.position = 'fixed'
  menuStyle.left = `${Math.round(r.left)}px`
  menuStyle.width = `${Math.round(r.width)}px`
  menuStyle.zIndex = '1000' // above modals (which sit around z-60)
  if (openUp) {
    menuStyle.top = 'auto'
    menuStyle.bottom = `${Math.round(window.innerHeight - r.top + gap)}px`
    menuStyle.maxHeight = `${Math.min(maxH, spaceAbove)}px`
  } else {
    menuStyle.bottom = 'auto'
    menuStyle.top = `${Math.round(r.bottom + gap)}px`
    menuStyle.maxHeight = `${Math.min(maxH, spaceBelow)}px`
  }
}

function onViewportChange() {
  if (open.value) updatePosition()
}

// Reposition while open on any scroll (capture=true catches scrolling ancestors) or resize.
watch(open, (isOpen) => {
  if (isOpen) {
    nextTick(updatePosition)
    window.addEventListener('scroll', onViewportChange, true)
    window.addEventListener('resize', onViewportChange)
  } else {
    window.removeEventListener('scroll', onViewportChange, true)
    window.removeEventListener('resize', onViewportChange)
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('scroll', onViewportChange, true)
  window.removeEventListener('resize', onViewportChange)
})

// Split a label around the matched query so the match can be emphasised.
function segments(label: string): { text: string; match: boolean }[] {
  const q = query.value.trim()
  if (!q) return [{ text: label, match: false }]
  const idx = label.toLowerCase().indexOf(q.toLowerCase())
  if (idx < 0) return [{ text: label, match: false }]
  return [
    { text: label.slice(0, idx), match: false },
    { text: label.slice(idx, idx + q.length), match: true },
    { text: label.slice(idx + q.length), match: false },
  ].filter((s) => s.text.length > 0)
}
</script>

<script lang="ts">
// Module-scoped counter for per-instance ids (kept out of setup so it's shared across instances).
const AppSelectCounter = { n: 0 }
</script>

<template>
  <div ref="rootEl" class="relative">
    <input
      :id="uid"
      ref="inputEl"
      type="text"
      role="combobox"
      autocomplete="off"
      spellcheck="false"
      :aria-expanded="open"
      :aria-controls="`${uid}-listbox`"
      :disabled="disabled"
      :value="displayValue"
      :placeholder="placeholderText"
      class="field-input cursor-pointer pr-16"
      :class="{ 'cursor-not-allowed opacity-60': disabled }"
      @focus="openMenu"
      @mousedown="openMenu"
      @input="onInput"
      @blur="closeMenu"
      @keydown.down.prevent="move(1)"
      @keydown.up.prevent="move(-1)"
      @keydown.enter.prevent="selectActive"
      @keydown.esc.prevent="closeMenu"
      @keydown.tab="closeMenu"
    />

    <!-- Trailing controls: clear (when there's a value) + chevron. -->
    <div class="pointer-events-none absolute inset-y-0 right-0 flex items-center gap-1 pr-2.5">
      <button
        v-if="clearable && model && !disabled"
        type="button"
        class="pointer-events-auto rounded p-0.5 text-text-muted transition-colors hover:text-text"
        :aria-label="t('common.clear')"
        tabindex="-1"
        @mousedown.prevent="clear"
      >
        <X :size="15" />
      </button>
      <ChevronDown
        :size="16"
        class="text-text-muted transition-transform"
        :class="{ 'rotate-180': open }"
      />
    </div>

    <!-- Popup listbox — teleported to <body> and fixed-positioned so it is never clipped by a
         scrolling table row or modal body. -->
    <Teleport to="body">
    <div
      v-if="open"
      :id="`${uid}-listbox`"
      ref="listboxEl"
      role="listbox"
      :style="menuStyle"
      class="overflow-y-auto rounded-card border border-border bg-surface p-1 shadow-card"
    >
      <p v-if="filtered.length === 0" class="px-3 py-6 text-center text-sm text-text-muted">
        {{ t('common.noResults') }}
      </p>
      <button
        v-for="(opt, i) in filtered"
        :key="opt.value"
        type="button"
        role="option"
        :aria-selected="opt.value === model"
        class="flex w-full items-center justify-between gap-2 rounded-control px-3 py-2 text-left text-sm transition-colors"
        :class="i === activeIndex ? 'bg-accent-soft text-text' : 'text-text hover:bg-surface-2'"
        @mousemove="activeIndex = i"
        @mousedown.prevent="choose(opt)"
      >
        <span class="truncate">
          <span
            v-for="(seg, s) in segments(opt.label)"
            :key="s"
            :class="{ 'font-semibold text-text': seg.match }"
            >{{ seg.text }}</span
          >
        </span>
        <span v-if="opt.value === model" class="shrink-0 text-xs text-text-muted">✓</span>
      </button>
    </div>
    </Teleport>
  </div>
</template>
