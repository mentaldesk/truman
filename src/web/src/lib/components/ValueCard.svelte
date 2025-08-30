<script lang="ts">
    export let value: {
        name: string;
        description: string;
        category: {
            emoji: string;
            name: string;
        };
    };
    export let draggable = true;
</script>

<div 
    class="bg-white p-3 rounded-lg shadow-sm border border-gray-200 hover:shadow-md transition-shadow duration-200"
    {draggable}
    on:dragstart
    on:dragend
>
    <div class="flex items-center gap-3">
        <div class="text-xl flex-shrink-0" aria-hidden="true">
            {value.category.emoji}
        </div>
        <div class="flex-1 min-w-0">
            <p class="text-sm text-gray-900">
                <span class="font-medium">{value.name}</span>
                <span class="text-gray-500"> - {value.description}</span>
            </p>
        </div>
    </div>
</div> 